using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace SmartDroneDelivery.Tests;

public class OrderReviewPostgresTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
{
    [PostgresFact]
    public async Task Competing_approvals_and_rejections_commit_exactly_one_decision()
    {
        using var client = factory.CreateClient();
        var actor = await ReviewData.UserAsync(factory.Services, "DISPATCHER");
        var order = await ReviewData.OrderAsync(factory.Services);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var requests = Enumerable.Range(0, 8).Select(async i =>
        {
            using var request = ReviewData.Request($"/api/delivery-orders/{order.Id}/" + (i % 2 == 0 ? "approve" : "reject"), actor.Token);
            if (i % 2 != 0) request.Content = JsonContent.Create(new { reason = "Closed" });
            await gate.Task;
            using var response = await client.SendAsync(request);
            return response.StatusCode;
        }).ToArray();
        gate.SetResult();
        var results = await Task.WhenAll(requests);
        Assert.Equal(1, results.Count(x => x == HttpStatusCode.OK));
        Assert.Equal(7, results.Count(x => x == HttpStatusCode.Conflict));
        await factory.WithDbAsync(async db =>
        {
            var saved = (await db.DeliveryOrders.FindAsync(order.Id))!;
            Assert.Contains(saved.Status, new[] { "APPROVED", "CANCELLED" });
            Assert.Equal(saved.Status == "APPROVED" ? actor.Id : (Guid?)null, saved.DispatcherId);
            Assert.Equal(1, await db.DeliveryStatusLogs.CountAsync(x => x.OrderId == order.Id));
            Assert.Equal(1, await db.Notifications.CountAsync(x => x.OrderId == order.Id));
            Assert.Equal(1, await db.AuditLogs.CountAsync(x => x.EntityId == order.Id.ToString()));
        });
    }

    [PostgresFact]
    public async Task Customer_cancellation_racing_approval_never_resurrects_cancelled_order()
    {
        using var client = factory.CreateClient();
        var actor = await ReviewData.UserAsync(factory.Services, "DISPATCHER");
        var order = await ReviewData.OrderAsync(factory.Services);
        var customerToken = await ReviewData.TokenAsync(factory.Services, order.CustomerId);
        using var approve = ReviewData.Request($"/api/delivery-orders/{order.Id}/approve", actor.Token);
        using var cancel = ReviewData.Request($"/api/delivery-orders/{order.Id}/cancel", customerToken);
        cancel.Content = JsonContent.Create(new { cancellationReason = "No longer needed" });
        var results = await Task.WhenAll(client.SendAsync(approve), client.SendAsync(cancel));
        try
        {
            Assert.Contains(results[0].StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.Conflict });
            // Cancellation may lose its stale PENDING CAS if approval committed while it loaded.
            Assert.Contains(results[1].StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.Conflict });
            if (results[1].StatusCode == HttpStatusCode.Conflict)
            {
                using var retry = ReviewData.Request($"/api/delivery-orders/{order.Id}/cancel", customerToken);
                retry.Content = JsonContent.Create(new { cancellationReason = "No longer needed" });
                using var response = await client.SendAsync(retry);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            }
            var approved = results[0].IsSuccessStatusCode;
            await ReviewData.AssertEffectsAsync(factory.Services, order.Id, "CANCELLED", approved ? 2 : 1, approved ? 1 : 0);
        }
        finally { foreach (var response in results) response.Dispose(); }
    }

    [PostgresFact]
    public async Task Approval_waits_for_station_edit_then_revalidates_committed_status()
    {
        using var client = factory.CreateClient();
        var actor = await ReviewData.UserAsync(factory.Services, "DISPATCHER");
        var order = await ReviewData.OrderAsync(factory.Services);
        await using var connection = new NpgsqlConnection(factory.DatabaseConnectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using (var command = new NpgsqlCommand("UPDATE landing_stations SET \"Status\" = 'MAINTENANCE' WHERE \"Id\" = @id", connection, transaction))
        {
            command.Parameters.AddWithValue("id", order.DestinationStationId);
            await command.ExecuteNonQueryAsync();
        }
        using var request = ReviewData.Request($"/api/delivery-orders/{order.Id}/approve", actor.Token);
        var pending = client.SendAsync(request);
        // Observe the actual database wait, rather than relying on a timing-only race.
        await using var monitor = new NpgsqlConnection(factory.DatabaseConnectionString);
        await monitor.OpenAsync();
        var blocked = false;
        for (var i = 0; i < 100 && !pending.IsCompleted; i++)
        {
            await using var command = new NpgsqlCommand("SELECT EXISTS (SELECT 1 FROM pg_stat_activity WHERE datname = current_database() AND wait_event_type = 'Lock' AND query LIKE '%landing_stations%')", monitor);
            if ((bool)(await command.ExecuteScalarAsync())!) { blocked = true; break; }
            await Task.Delay(20);
        }
        await transaction.CommitAsync();
        using var response = await pending;
        Assert.True(blocked, "Approval must lock the stations until the decision transaction ends.");
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("Destination", await response.Content.ReadAsStringAsync());
        await ReviewData.AssertEffectsAsync(factory.Services, order.Id, "PENDING", 0, 0);
    }

    [PostgresFact]
    public async Task Notification_insert_failure_rolls_back_cas_dispatcher_log_and_audit()
    {
        using var client = factory.CreateClient();
        var actor = await ReviewData.UserAsync(factory.Services, "DISPATCHER");
        var order = await ReviewData.OrderAsync(factory.Services);
        await using var connection = new NpgsqlConnection(factory.DatabaseConnectionString);
        await connection.OpenAsync();
        var suffix = Guid.NewGuid().ToString("N");
        var function = $"review_fail_{suffix}";
        await using (var command = new NpgsqlCommand($"""
            CREATE FUNCTION {function}() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                IF NEW."OrderId" = '{order.Id}'::uuid THEN RAISE EXCEPTION 'Injected review notification failure'; END IF;
                RETURN NEW;
            END; $$;
            CREATE TRIGGER {function} BEFORE INSERT ON notifications FOR EACH ROW EXECUTE FUNCTION {function}();
            """, connection)) await command.ExecuteNonQueryAsync();
        try
        {
            using var request = ReviewData.Request($"/api/delivery-orders/{order.Id}/approve", actor.Token);
            using var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
            await ReviewData.AssertEffectsAsync(factory.Services, order.Id, "PENDING", 0, 0);
            await factory.WithDbAsync(async db => Assert.Null((await db.DeliveryOrders.FindAsync(order.Id))!.DispatcherId));
        }
        finally
        {
            await using var command = new NpgsqlCommand($"DROP TRIGGER {function} ON notifications; DROP FUNCTION {function}();", connection);
            await command.ExecuteNonQueryAsync();
        }
    }
}

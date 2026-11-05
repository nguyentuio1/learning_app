using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SmartDroneDelivery.Api.Entities;
using SmartDroneDelivery.Api.Security;
using SmartDroneDelivery.Api.Services;
using Xunit.Abstractions;

namespace SmartDroneDelivery.Tests;

/// <summary>
/// LA-322: PUT /api/delivery-orders/{id} and POST /api/delivery-orders/{id}/cancel guard against concurrent
/// requests with a conditional update and a transaction. That code only runs on a relational database, so these
/// tests run on PostgreSQL (see <see cref="PostgresApiFactory"/>) and are skipped when no server is configured.
///
/// The requests are really sent at the same moment (they wait at a gate and are released together), and each case
/// repeats with a fresh order. The assertions are about what must hold in every possible order of events, never
/// about which request wins.
/// </summary>
public class DeliveryOrderConcurrencyTests : IClassFixture<PostgresApiFactory>, IDisposable
{
    private const string Password = "Passw0rd123";
    private const string OriginalName = "Original Recipient";
    private const int Rounds = 15;
    private static readonly string PasswordHash = new PasswordHasher().Hash(Password);
    private static int _counter;

    private readonly PostgresApiFactory _factory;
    private readonly ITestOutputHelper _output;
    private readonly HttpClient _client;

    public DeliveryOrderConcurrencyTests(PostgresApiFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _output = output;
        // Constructing the class fixture does not start the host; skipped tests never reach this client.
        _client = PostgresApiFactory.ServerConnectionString is null ? new HttpClient() : factory.CreateClient();
    }

    public void Dispose() => _client.Dispose();

    // ---------- scenarios ----------

    [PostgresFact]
    public async Task Several_cancel_requests_at_the_same_time_cancel_the_order_once()
    {
        var (customerId, token) = await SeedCustomerAsync();
        var (originId, destId, _) = await SeedStationsAsync();
        const int simultaneous = 8;

        for (var round = 1; round <= Rounds; round++)
        {
            var orderId = await SeedOrderAsync(customerId, originId, destId, "PENDING");

            var responses = await FireTogetherAsync(Enumerable.Range(0, simultaneous)
                .Select(i => (Func<Task<HttpResponseMessage>>)(() => CancelAsync(orderId, token, $"Reason {round}-{i}"))).ToArray());
            var codes = responses.Select(r => r.StatusCode).ToList();

            Assert.True(codes.Count(c => c == HttpStatusCode.OK) == 1,
                $"round {round}: expected exactly one 200 but got {string.Join(", ", codes)}");
            Assert.True(codes.Count(c => c == HttpStatusCode.Conflict) == simultaneous - 1,
                $"round {round}: the other requests must be 409 but got {string.Join(", ", codes)}");

            await _factory.WithDbAsync(async db =>
            {
                Assert.Equal("CANCELLED", (await db.DeliveryOrders.AsNoTracking().SingleAsync(o => o.Id == orderId)).Status);
                Assert.Equal(1, await db.DeliveryStatusLogs.CountAsync(l => l.OrderId == orderId && l.ToStatus == "CANCELLED"));
                Assert.Equal(1, await db.Notifications.CountAsync(n => n.OrderId == orderId));
            });
        }
        _output.WriteLine($"{Rounds} rounds x {simultaneous} simultaneous cancels: exactly one 200, {simultaneous - 1} x 409, one status log and one notification each round.");
    }

    [PostgresFact]
    public async Task A_cancel_and_an_update_at_the_same_time_never_leave_a_cancelled_order_changed_afterwards()
    {
        var (customerId, token) = await SeedCustomerAsync();
        var (originId, destId, altDestId) = await SeedStationsAsync();
        var updateWon = 0;
        var cancelWon = 0;

        for (var round = 1; round <= Rounds; round++)
        {
            var orderId = await SeedOrderAsync(customerId, originId, destId, "PENDING");
            var newName = $"Changed {round}";

            var responses = await FireTogetherAsync(
                () => CancelAsync(orderId, token, "Recipient is not available."),
                () => UpdateAsync(orderId, token, newName, altDestId));
            var cancel = responses[0].StatusCode;
            var update = responses[1].StatusCode;

            // Cancelling a PENDING order is valid both before and after an update, so it always succeeds.
            Assert.True(cancel == HttpStatusCode.OK, $"round {round}: cancel returned {cancel}, update returned {update}");
            // An update is either applied before the cancel or refused with 409, never anything else.
            Assert.True(update is HttpStatusCode.OK or HttpStatusCode.Conflict, $"round {round}: update returned {update}");

            await _factory.WithDbAsync(async db =>
            {
                var order = await db.DeliveryOrders.AsNoTracking().SingleAsync(o => o.Id == orderId);
                Assert.Equal("CANCELLED", order.Status);
                Assert.Equal(1, await db.DeliveryStatusLogs.CountAsync(l => l.OrderId == orderId && l.ToStatus == "CANCELLED"));
                Assert.Equal(1, await db.Notifications.CountAsync(n => n.OrderId == orderId));
                if (update == HttpStatusCode.Conflict)
                {
                    // The refused update must not have changed anything.
                    Assert.Equal(OriginalName, order.RecipientName);
                    Assert.Equal(destId, order.DestinationStationId);
                }
                else
                {
                    Assert.Equal(newName, order.RecipientName);
                    Assert.Equal(altDestId, order.DestinationStationId);
                }
            });

            if (update == HttpStatusCode.OK) updateWon++; else cancelWon++;
        }
        _output.WriteLine($"{Rounds} rounds: update applied first in {updateWon}, cancel first (update refused with 409) in {cancelWon}. Cancel was 200 and the order CANCELLED in every round.");
    }

    [PostgresFact]
    public async Task An_update_after_the_order_left_PENDING_is_refused_and_changes_nothing()
    {
        var (customerId, token) = await SeedCustomerAsync();
        var (originId, destId, altDestId) = await SeedStationsAsync();
        var orderId = await SeedOrderAsync(customerId, originId, destId, "APPROVED");

        var response = await UpdateAsync(orderId, token, "Should not be saved", altDestId);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await _factory.WithDbAsync(async db =>
        {
            var order = await db.DeliveryOrders.AsNoTracking().SingleAsync(o => o.Id == orderId);
            Assert.Equal("APPROVED", order.Status);
            Assert.Equal(OriginalName, order.RecipientName);
            Assert.Equal(destId, order.DestinationStationId);
        });
    }

    [PostgresFact]
    public async Task Updates_racing_with_the_order_leaving_PENDING_are_either_applied_before_or_refused()
    {
        var (customerId, token) = await SeedCustomerAsync();
        var (originId, destId, altDestId) = await SeedStationsAsync();
        const int updates = 6;
        var appliedBeforeLeaving = 0;
        var refused = 0;

        for (var round = 1; round <= Rounds; round++)
        {
            var orderId = await SeedOrderAsync(customerId, originId, destId, "PENDING");
            var calls = Enumerable.Range(0, updates)
                .Select(i => (Func<Task<HttpResponseMessage>>)(() => UpdateAsync(orderId, token, $"Racer {round}-{i}", altDestId)))
                // The order is approved (for example by a dispatcher) while the updates are in flight.
                .Append(async () =>
                {
                    // One SQL statement always beats the several queries of an update, so wait a random moment to
                    // let the approval land at different points of the updates (both outcomes are valid).
                    await Task.Delay(Random.Shared.Next(0, 40));
                    await _factory.WithDbAsync(async db =>
                        await db.DeliveryOrders.Where(o => o.Id == orderId)
                            .ExecuteUpdateAsync(s => s.SetProperty(o => o.Status, "APPROVED")));
                    return new HttpResponseMessage(HttpStatusCode.NoContent);
                })
                .ToArray();

            var responses = await FireTogetherAsync(calls);
            var updateCodes = responses.Take(updates).Select(r => r.StatusCode).ToList();

            Assert.All(updateCodes, c => Assert.True(c is HttpStatusCode.OK or HttpStatusCode.Conflict, $"round {round}: update returned {c}"));
            await _factory.WithDbAsync(async db =>
            {
                var order = await db.DeliveryOrders.AsNoTracking().SingleAsync(o => o.Id == orderId);
                Assert.Equal("APPROVED", order.Status);
                if (updateCodes.All(c => c == HttpStatusCode.Conflict))
                    Assert.Equal(OriginalName, order.RecipientName);
                else
                    Assert.StartsWith($"Racer {round}-", order.RecipientName);
            });

            appliedBeforeLeaving += updateCodes.Count(c => c == HttpStatusCode.OK);
            refused += updateCodes.Count(c => c == HttpStatusCode.Conflict);
        }
        _output.WriteLine($"{Rounds} rounds x {updates} updates racing with the approval: {appliedBeforeLeaving} applied before the order left PENDING, {refused} refused with 409, no other result.");
    }

    // The two tests below remove the luck from the race. They hold the order row locked, send the request, wait
    // until it is stuck on that lock (so it has already read the order as PENDING and passed every check),
    // change the order in the meantime and release the lock. Only the conditional UPDATE can still refuse the
    // request at that point; the check made earlier in the C# code cannot.

    [PostgresFact]
    public async Task An_update_that_already_read_a_PENDING_order_is_refused_when_the_order_is_approved_before_it_writes()
    {
        var (customerId, token) = await SeedCustomerAsync();
        var (originId, destId, altDestId) = await SeedStationsAsync();
        var orderId = await SeedOrderAsync(customerId, originId, destId, "PENDING");

        var response = await SendWhileRowIsLockedAsync(orderId,
            () => UpdateAsync(orderId, token, "Should not be saved", altDestId),
            "UPDATE delivery_orders SET \"Status\" = 'APPROVED' WHERE \"Id\" = @id");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await _factory.WithDbAsync(async db =>
        {
            var order = await db.DeliveryOrders.AsNoTracking().SingleAsync(o => o.Id == orderId);
            Assert.Equal("APPROVED", order.Status);
            Assert.Equal(OriginalName, order.RecipientName);
            Assert.Equal(destId, order.DestinationStationId);
        });
    }

    [PostgresFact]
    public async Task A_cancel_that_already_read_a_PENDING_order_is_refused_when_another_cancel_commits_first()
    {
        var (customerId, token) = await SeedCustomerAsync();
        var (originId, destId, _) = await SeedStationsAsync();
        var orderId = await SeedOrderAsync(customerId, originId, destId, "PENDING");

        var response = await SendWhileRowIsLockedAsync(orderId,
            () => CancelAsync(orderId, token, "Second cancel"),
            "UPDATE delivery_orders SET \"Status\" = 'CANCELLED', \"CancelReason\" = 'First cancel' WHERE \"Id\" = @id");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await _factory.WithDbAsync(async db =>
        {
            var order = await db.DeliveryOrders.AsNoTracking().SingleAsync(o => o.Id == orderId);
            Assert.Equal("CANCELLED", order.Status);
            Assert.Equal("First cancel", order.CancelReason);
            // The refused cancel rolled back: no second status log and no notification.
            Assert.Equal(0, await db.DeliveryStatusLogs.CountAsync(l => l.OrderId == orderId && l.ToStatus == "CANCELLED"));
            Assert.Equal(0, await db.Notifications.CountAsync(n => n.OrderId == orderId));
        });
    }

    // ---------- helpers ----------

    /// <summary>
    /// Locks the order row, sends the request, waits until the request is blocked on that lock, runs
    /// <paramref name="sqlWhileLocked"/> (it can use @id) and commits. Returns the response of the request.
    /// </summary>
    private async Task<HttpResponseMessage> SendWhileRowIsLockedAsync(Guid orderId, Func<Task<HttpResponseMessage>> send, string sqlWhileLocked)
    {
        await using var connection = new Npgsql.NpgsqlConnection(_factory.DatabaseConnectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        await using (var lockRow = new Npgsql.NpgsqlCommand("SELECT 1 FROM delivery_orders WHERE \"Id\" = @id FOR UPDATE", connection, transaction))
        {
            lockRow.Parameters.AddWithValue("id", orderId);
            await lockRow.ExecuteScalarAsync();
        }

        var request = Task.Run(send);
        await WaitUntilABackendWaitsForALockAsync();

        await using (var change = new Npgsql.NpgsqlCommand(sqlWhileLocked, connection, transaction))
        {
            change.Parameters.AddWithValue("id", orderId);
            await change.ExecuteNonQueryAsync();
        }
        await transaction.CommitAsync();

        return await request.WaitAsync(TimeSpan.FromSeconds(30));
    }

    private async Task WaitUntilABackendWaitsForALockAsync()
    {
        await using var probe = new Npgsql.NpgsqlConnection(_factory.DatabaseConnectionString);
        await probe.OpenAsync();
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < deadline)
        {
            await using var command = new Npgsql.NpgsqlCommand(
                "SELECT count(*) FROM pg_stat_activity WHERE datname = @db AND wait_event_type = 'Lock'", probe);
            command.Parameters.AddWithValue("db", _factory.DatabaseName);
            if ((long)(await command.ExecuteScalarAsync())! > 0) return;
            await Task.Delay(20);
        }
        throw new TimeoutException("The request never reached the row lock; the test would not prove anything.");
    }

    /// <summary>Starts every call at the same moment: all of them wait at a gate which is then opened.</summary>
    private static async Task<HttpResponseMessage[]> FireTogetherAsync(params Func<Task<HttpResponseMessage>>[] calls)
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tasks = calls.Select(call => Task.Run(async () =>
        {
            await gate.Task;
            return await call();
        })).ToArray();
        await Task.Delay(100); // let every task reach the gate
        gate.SetResult();
        return await Task.WhenAll(tasks);
    }

    private Task<HttpResponseMessage> CancelAsync(Guid orderId, string token, string reason)
    {
        var request = Authorized(HttpMethod.Post, $"/api/delivery-orders/{orderId}/cancel", token);
        request.Content = JsonContent.Create(new { cancellationReason = reason });
        return _client.SendAsync(request);
    }

    private Task<HttpResponseMessage> UpdateAsync(Guid orderId, string token, string recipientName, Guid destinationStationId)
    {
        var request = Authorized(HttpMethod.Put, $"/api/delivery-orders/{orderId}", token);
        request.Content = JsonContent.Create(new
        {
            recipientName,
            recipientPhone = "0912345678",
            destinationStationId,
            deliveryAddressId = (Guid?)null
        });
        return _client.SendAsync(request);
    }

    private static HttpRequestMessage Authorized(HttpMethod method, string path, string token)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private async Task<(Guid UserId, string Token)> SeedCustomerAsync()
    {
        var n = Interlocked.Increment(ref _counter);
        var email = $"concurrency{n}@example.com";
        var userId = Guid.NewGuid();
        await _factory.WithDbAsync(async db =>
        {
            var role = await db.Roles.SingleAsync(r => r.Code == AppRoles.Customer);
            db.Users.Add(new User
            {
                Id = userId, Email = email, FullName = $"Concurrency User {n}", PhoneNumber = $"08{n:D8}",
                PasswordHash = PasswordHash, RoleId = role.Id, Status = "ACTIVE"
            });
            await db.SaveChangesAsync();
        });

        using var login = await _client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var body = await login.Content.ReadFromJsonAsync<JsonElement>();
        return (userId, body.GetProperty("accessToken").GetString()!);
    }

    private async Task<(Guid OriginId, Guid DestId, Guid AltDestId)> SeedStationsAsync()
    {
        var ids = (Origin: Guid.NewGuid(), Dest: Guid.NewGuid(), Alt: Guid.NewGuid());
        await _factory.WithDbAsync(async db =>
        {
            LandingStation Station(Guid id, string name, decimal lat) => new()
            {
                Id = id, StationCode = $"C-{Guid.NewGuid().ToString("N")[..6].ToUpper()}", Name = name,
                Address = $"{name} Street", Latitude = lat, Longitude = 106.70m, Status = "OPERATIONAL"
            };
            db.LandingStations.AddRange(Station(ids.Origin, "Origin Hub", 10.77m), Station(ids.Dest, "Dest Hub", 10.78m), Station(ids.Alt, "Alt Hub", 10.79m));
            await db.SaveChangesAsync();
        });
        return (ids.Origin, ids.Dest, ids.Alt);
    }

    private async Task<Guid> SeedOrderAsync(Guid customerId, Guid originId, Guid destId, string status)
    {
        var packageId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        await _factory.WithDbAsync(async db =>
        {
            db.Packages.Add(new Package
            {
                Id = packageId, SenderId = customerId, PackageName = "Gift Box", WeightGram = 800m,
                LengthCm = 20m, WidthCm = 15m, HeightCm = 10m, IsFragile = true, CreatedAt = now
            });
            db.DeliveryOrders.Add(new DeliveryOrder
            {
                Id = orderId, TrackingNumber = $"DD-202611-{Interlocked.Increment(ref _counter):D4}", CustomerId = customerId,
                PackageId = packageId, OriginStationId = originId, DestinationStationId = destId,
                RecipientName = OriginalName, RecipientPhone = "0987654321", Status = status,
                PickupSecureCode = "483920", CreatedAt = now, UpdatedAt = now
            });
            db.DeliveryStatusLogs.Add(new DeliveryStatusLog
            {
                OrderId = orderId, ChangedBy = customerId, FromStatus = null, ToStatus = "PENDING",
                Remarks = "Order created", CreatedAt = now
            });
            await db.SaveChangesAsync();
        });
        return orderId;
    }
}

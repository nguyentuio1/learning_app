using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartDroneDelivery.Api.Entities;
using SmartDroneDelivery.Api.Services;

namespace SmartDroneDelivery.Tests;

public class OrderTransitionPostgresTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
{
    [PostgresFact]
    public async Task Racing_transitions_have_one_winner_and_one_log_and_notification()
    {
        var id = await SeedOrderAsync();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var attempts = Enumerable.Range(0, 8).Select(async _ =>
        {
            using var scope = factory.Services.CreateScope();
            var writer = scope.ServiceProvider.GetRequiredService<IOrderTransitionWriter>();
            await gate.Task;
            try
            {
                await writer.ApplyAsync(id, "PENDING", "APPROVED", null, "Checked");
                return 200;
            }
            catch (ApiException error) { return error.StatusCode; }
        }).ToArray();
        gate.SetResult();
        var results = await Task.WhenAll(attempts);
        Assert.Equal(1, results.Count(x => x == 200));
        Assert.Equal(7, results.Count(x => x == 409));
        await factory.WithDbAsync(async db =>
        {
            Assert.Equal("APPROVED", (await db.DeliveryOrders.FindAsync(id))!.Status);
            Assert.Equal(1, await db.DeliveryStatusLogs.CountAsync(x => x.OrderId == id));
            Assert.Equal(1, await db.Notifications.CountAsync(x => x.OrderId == id));
        });
    }

    [PostgresFact]
    public async Task Log_persistence_failure_rolls_back_status_and_customer_notification()
    {
        var id = await SeedOrderAsync();
        using (var scope = factory.Services.CreateScope())
        {
            var writer = scope.ServiceProvider.GetRequiredService<IOrderTransitionWriter>();
            // A nonexistent actor violates the log FK after the status CAS has succeeded.
            await Assert.ThrowsAsync<DbUpdateException>(() =>
                writer.ApplyAsync(id, "PENDING", "APPROVED", Guid.NewGuid(), "Invalid actor"));
        }
        await factory.WithDbAsync(async db =>
        {
            Assert.Equal("PENDING", (await db.DeliveryOrders.FindAsync(id))!.Status);
            Assert.Equal(0, await db.DeliveryStatusLogs.CountAsync(x => x.OrderId == id));
            Assert.Equal(0, await db.Notifications.CountAsync(x => x.OrderId == id));
        });
    }

    private async Task<Guid> SeedOrderAsync()
    {
        var id = Guid.NewGuid();
        await factory.WithDbAsync(async db =>
        {
            var unique = Guid.NewGuid().ToString("N");
            var customer = new User
            {
                RoleId = (await db.Roles.FirstAsync(x => x.Code == "CUSTOMER")).Id,
                Email = $"transition-{unique}@example.com", PhoneNumber = unique[..15], FullName = "Transition customer"
            };
            var order = new DeliveryOrder
            {
                Id = id, TrackingNumber = $"T-{unique}", Customer = customer, Status = "PENDING",
                Package = new Package { Sender = customer, PackageName = "Books", WeightGram = 450 },
                OriginStation = new LandingStation { StationCode = $"O-{unique[..8]}", Name = "Origin" },
                DestinationStation = new LandingStation { StationCode = $"D-{unique[..8]}", Name = "Destination" }
            };
            db.Add(order);
            await db.SaveChangesAsync();
        });
        return id;
    }
}

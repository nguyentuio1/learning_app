using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using SmartDroneDelivery.Api.Data;
using SmartDroneDelivery.Api.Entities;
using SmartDroneDelivery.Api.Security;
using SmartDroneDelivery.Api.Services;

namespace SmartDroneDelivery.Tests;

public class SummaryPostgresTests
{
    [PostgresFact]
    public async Task Consistent_ordered_history_is_read_without_a_transaction_during_generation_and_stale_logs_are_discarded()
    {
        using var fixture = new PostgresApiFactory();
        var model = new SummaryTestModel();
        using var app = fixture.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<ISummaryModelGateway>(); services.AddSingleton<ISummaryModelGateway>(model);
        }));
        using var client = app.CreateClient();
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var customer = new User { Email = $"sum-pg-{Guid.NewGuid():N}@example.com", FullName = "Private person",
            PasswordHash = "unused", PhoneNumber = "0900000000", Status = "ACTIVE",
            RoleId = (await db.Roles.SingleAsync(r => r.Code == AppRoles.Customer)).Id };
        var a = new LandingStation { StationCode = $"S-{Guid.NewGuid():N}"[..12], Name = "Origin", Address = "Private address" };
        var b = new LandingStation { StationCode = $"S-{Guid.NewGuid():N}"[..12], Name = "Destination", Address = "Private address" };
        var package = new Package { SenderId = customer.Id, PackageName = "Parcel", WeightGram = 100 };
        var order = new DeliveryOrder { CustomerId = customer.Id, PackageId = package.Id, OriginStationId = a.Id,
            DestinationStationId = b.Id, TrackingNumber = $"S-{Guid.NewGuid():N}"[..20],
            RecipientName = "Private recipient", RecipientPhone = "0909999999", PickupSecureCode = "654321", Status = "ARRIVED" };
        db.Users.Add(customer); db.LandingStations.AddRange(a, b); db.Packages.Add(package); db.DeliveryOrders.Add(order);
        await db.SaveChangesAsync();
        var time = new DateTime(2026, 11, 16, 8, 0, 0, DateTimeKind.Utc);
        // Same timestamps deliberately force the numeric ID tie breaker, regardless of insertion order.
        db.DeliveryStatusLogs.AddRange(
            new DeliveryStatusLog { Id = 200, OrderId = order.Id, FromStatus = "READY_FOR_TAKEOFF", ToStatus = "IN_TRANSIT", CreatedAt = time },
            new DeliveryStatusLog { Id = 100, OrderId = order.Id, FromStatus = "APPROVED", ToStatus = "READY_FOR_TAKEOFF", CreatedAt = time },
            new DeliveryStatusLog { Id = 300, OrderId = order.Id, FromStatus = "IN_TRANSIT", ToStatus = "ARRIVED", CreatedAt = time, Remarks = "SECRET 654321" });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        model.Handler = async (input, ct) =>
        {
            Assert.Equal(new[] { "READY_FOR_TAKEOFF", "IN_TRANSIT", "ARRIVED" }, input.Events.Select(e => e.ToStatus));
            await using var connection = new NpgsqlConnection(fixture.DatabaseConnectionString);
            await connection.OpenAsync(ct);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT count(*) FROM pg_stat_activity WHERE datname=current_database() AND state='idle in transaction'";
            Assert.Equal(0L, (long)(await command.ExecuteScalarAsync(ct))!);
            return [0, 1, 2];
        };
        var service = scope.ServiceProvider.GetRequiredService<DeliverySummaryService>();
        var generated = await service.GenerateAsync(customer.Id, order.Id, default);
        Assert.Equal("GENERATED", generated.Outcome); Assert.Contains("awaiting pickup", generated.Text);
        Assert.DoesNotContain("654321", generated.Text);
        Assert.Equal(3, await db.DeliveryStatusLogs.CountAsync(l => l.OrderId == order.Id));
        Assert.Empty(await db.AiChatSessions.ToArrayAsync());

        var previousOrderTime = (await db.DeliveryOrders.AsNoTracking().SingleAsync(o => o.Id == order.Id)).UpdatedAt;
        model.Handler = async (input, ct) =>
        {
            using var editingScope = app.Services.CreateScope();
            var editingDb = editingScope.ServiceProvider.GetRequiredService<AppDbContext>();
            await editingDb.DeliveryStatusLogs.Where(l => l.Id == 300)
                .ExecuteUpdateAsync(s => s.SetProperty(l => l.ToStatus, "FAILED"), ct);
            return [0, 1, 2];
        };
        var stale = await Assert.ThrowsAsync<ApiException>(() => service.GenerateAsync(customer.Id, order.Id, default));
        Assert.Equal(409, stale.StatusCode);
        Assert.Equal(previousOrderTime, (await db.DeliveryOrders.AsNoTracking().SingleAsync(o => o.Id == order.Id)).UpdatedAt);
        Assert.Equal("ARRIVED", (await db.DeliveryOrders.AsNoTracking().SingleAsync(o => o.Id == order.Id)).Status);
    }
}

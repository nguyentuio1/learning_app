using System.Net;
using Microsoft.EntityFrameworkCore;
using SmartDroneDelivery.Api.Services;

namespace SmartDroneDelivery.Tests;

public class ExpiredParcelClearingPostgresTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
{
    [PostgresFact]
    public async Task Concurrent_clears_release_and_audit_exactly_once()
    {
        using var client = factory.CreateClient();
        var s = await ExpiredParcelData.SeedAsync(factory.Services);
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<HttpResponseMessage> SendAsync() { await start.Task; return await ExpiredParcelData.ClearAsync(client, s); }
        var first = SendAsync(); var second = SendAsync();
        start.SetResult();
        var responses = await Task.WhenAll(first, second);
        try {
            Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK);
            Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Conflict);
            await factory.WithDbAsync(async db => {
                Assert.Equal("EMPTY", (await db.StationSlots.FindAsync(s.SlotId))!.Status);
                Assert.Equal("FAILED", (await db.DeliveryOrders.FindAsync(s.Order.Id))!.Status);
                var log = Assert.Single(await db.DeliveryStatusLogs.Where(l => l.OrderId == s.Order.Id).ToListAsync());
                Assert.Equal(1, log.Remarks!.Split("Moved parcel to holding rack.").Length - 1);
                Assert.Equal(1, await db.AuditLogs.CountAsync(a => a.EntityId == s.SlotId.ToString() && a.Action == "EXPIRED_PARCEL_CLEARED"));
                Assert.Equal("Moved parcel to holding rack.", await db.AuditLogs.Where(a => a.EntityId == s.SlotId.ToString()).Select(a => EF.Property<string>(a, "Reason")).SingleAsync());
            });
        } finally { foreach (var response in responses) response.Dispose(); }
    }

    [PostgresFact]
    public async Task Failed_audit_insert_rolls_back_slot_order_and_status_log()
    {
        using var client = factory.CreateClient();
        var s = await ExpiredParcelData.SeedAsync(factory.Services);
        DateTime before = default;
        await factory.WithDbAsync(async db => {
            before = (await db.DeliveryOrders.FindAsync(s.Order.Id))!.UpdatedAt;
            await db.Database.ExecuteSqlRawAsync("""
                CREATE OR REPLACE FUNCTION reject_la336_audit() RETURNS trigger AS $$
                BEGIN
                    IF NEW."Action" = 'EXPIRED_PARCEL_CLEARED' THEN
                        RAISE EXCEPTION 'Injected audit insert failure';
                    END IF;
                    RETURN NEW;
                END;
                $$ LANGUAGE plpgsql;
                CREATE TRIGGER reject_la336_audit BEFORE INSERT ON audit_logs
                    FOR EACH ROW EXECUTE FUNCTION reject_la336_audit();
                """);
        });
        try {
            using var response = await ExpiredParcelData.ClearAsync(client, s);
            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
            await factory.WithDbAsync(async db => {
                Assert.Equal("OCCUPIED", (await db.StationSlots.FindAsync(s.SlotId))!.Status);
                var order = (await db.DeliveryOrders.FindAsync(s.Order.Id))!;
                Assert.Equal("FAILED", order.Status);
                Assert.Equal(before, order.UpdatedAt);
                Assert.Equal(PickupTimeoutService.LogRemarks, (await db.DeliveryStatusLogs.SingleAsync(l => l.OrderId == s.Order.Id)).Remarks);
                Assert.False(await db.AuditLogs.AnyAsync(a => a.EntityId == s.SlotId.ToString()));
            });
        } finally {
            await factory.WithDbAsync(db => db.Database.ExecuteSqlRawAsync("DROP TRIGGER reject_la336_audit ON audit_logs; DROP FUNCTION reject_la336_audit();"));
        }
        using var retry = await ExpiredParcelData.ClearAsync(client, s);
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
    }
}

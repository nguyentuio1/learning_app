using System.Data.Common;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SmartDroneDelivery.Api.Data;
using SmartDroneDelivery.Api.DTOs;
using SmartDroneDelivery.Api.Entities;
using SmartDroneDelivery.Api.Services;

namespace SmartDroneDelivery.Tests;

public class AnalyticsPostgresTests
{
    [PostgresFact]
    public async Task Relational_aggregates_match_HTTP_contract_and_preserve_operational_data()
    {
        using var factory = new PostgresApiFactory(); using var client = factory.CreateClient();
        var user = await AnalyticsApiTests.AuthorizeAsync(factory.Services,client);
        await AnalyticsApiTests.AssertEmptyAsync(client);
        await factory.WithDbAsync(db => AnalyticsApiTests.SeedCohortAsync(db,user));
        await AnalyticsApiTests.AssertCohortAsync(client);
        await factory.WithDbAsync(async db => {
            Assert.Equal(8, await db.DeliveryOrders.CountAsync());
            Assert.Equal(16, await db.DeliveryStatusLogs.CountAsync());
            Assert.Equal(0, await db.Notifications.CountAsync()); Assert.Equal(0, await db.AuditLogs.CountAsync());
        });
    }

    [PostgresFact]
    public async Task Concurrent_delivery_change_does_not_mix_snapshot_metrics()
    {
        using var factory = new PostgresApiFactory(); using var client = factory.CreateClient();
        var user = await AnalyticsApiTests.AuthorizeAsync(factory.Services,client);
        await factory.WithDbAsync(db => AnalyticsApiTests.SeedCohortAsync(db,user));
        var pause = new PauseStatusCounts();
        await using var readDb = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(factory.DatabaseConnectionString).AddInterceptors(pause).Options);
        var service = new AnalyticsQueryService(readDb,TimeProvider.System);
        var query = new AnalyticsQuery(new ReportingPeriod("2026-11-15","2026-11-15","Asia/Ho_Chi_Minh",new(2026,11,14,17,0,0,DateTimeKind.Utc),new(2026,11,15,17,0,0,DateTimeKind.Utc)));
        var resultTask = service.DashboardAsync(user,query);
        try {
            await pause.Reached.Task.WaitAsync(TimeSpan.FromSeconds(15));
            await factory.WithDbAsync(async db => {
                var order = await db.DeliveryOrders.SingleAsync(o => o.TrackingNumber == "AN-0");
                order.Status = "FAILED"; await db.SaveChangesAsync();
            });
        } finally { pause.Release.TrySetResult(); }
        var result = await resultTask.WaitAsync(TimeSpan.FromSeconds(15));
        Assert.Equal(3,result.SuccessRate.DeliveredCount); Assert.Equal(75m,result.SuccessRate.Percent);
        Assert.Equal(1,result.AverageDelivery.SampleCount); Assert.Equal(12.5m,result.AverageDelivery.Minutes);
        using var refreshed = await client.GetAsync(AnalyticsApiTests.Dashboard + AnalyticsApiTests.Query);
        var body = await refreshed.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2,body.GetProperty("successRate").GetProperty("deliveredCount").GetInt32());
        Assert.Equal(50,body.GetProperty("successRate").GetProperty("percent").GetDecimal());
        Assert.Equal(JsonValueKind.Null,body.GetProperty("averageDelivery").GetProperty("minutes").ValueKind);
    }

    [PostgresFact]
    public async Task Same_station_is_counted_once_and_report_ties_have_stable_id_order()
    {
        using var factory = new PostgresApiFactory(); using var client = factory.CreateClient();
        var user = await AnalyticsApiTests.AuthorizeAsync(factory.Services,client);
        await factory.WithDbAsync(db => AnalyticsApiTests.SeedCohortAsync(db,user));
        var low = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var high = Guid.Parse("00000000-0000-0000-0000-000000000002");
        await factory.WithDbAsync(async db => {
            var orders = await db.DeliveryOrders.Where(o => o.TrackingNumber == "AN-4" || o.TrackingNumber == "AN-5").OrderBy(o => o.TrackingNumber).ToListAsync();
            // New rows avoid modifying primary keys; same-station legacy data is legal in the database.
            foreach (var (id,index) in new[] {(low,0),(high,1)}) {
                var original = orders[index];
                db.DeliveryOrders.Add(new DeliveryOrder { Id=id,TrackingNumber=$"TIE-{index}",CustomerId=user,Package=new Package {SenderId=user,PackageName="Tie"},OriginStationId=original.OriginStationId,DestinationStationId=original.OriginStationId,Status="PENDING",CreatedAt=new(2026,11,15,16,0,0,DateTimeKind.Utc),PickupSecureCode="654321" });
            }
            await db.SaveChangesAsync();
        });
        using var dashboard = await client.GetAsync(AnalyticsApiTests.Dashboard + AnalyticsApiTests.Query);
        var body = await dashboard.Content.ReadFromJsonAsync<JsonElement>();
        var a = body.GetProperty("stationUsage").EnumerateArray().Single(s=>s.GetProperty("stationCode").GetString()=="AN-A");
        Assert.Equal(8,a.GetProperty("originOrderCount").GetInt32()); Assert.Equal(2,a.GetProperty("destinationOrderCount").GetInt32()); Assert.Equal(8,a.GetProperty("totalDistinctOrders").GetInt32());
        using var report = await client.GetAsync(AnalyticsApiTests.Report + AnalyticsApiTests.Query + "&pageSize=1");
        var page = await report.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(high,page.GetProperty("items")[0].GetProperty("orderId").GetGuid());
        using var next = await client.GetAsync(AnalyticsApiTests.Report + AnalyticsApiTests.Query + "&page=2&pageSize=1");
        Assert.Equal(low,(await next.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("items")[0].GetProperty("orderId").GetGuid());
    }

    [PostgresFact]
    public async Task Five_thousand_order_dashboard_returns_correct_totals_within_target()
    {
        using var factory = new PostgresApiFactory(); using var client = factory.CreateClient();
        var user = await AnalyticsApiTests.AuthorizeAsync(factory.Services,client);
        await factory.WithDbAsync(async db => {
            var station = new LandingStation {StationCode="PERF",Name="Perf",Address="test"};
            for(var i=0;i<5000;i++) db.DeliveryOrders.Add(new DeliveryOrder {CustomerId=user,Package=new Package {SenderId=user,PackageName="Load"},OriginStation=station,DestinationStation=station,TrackingNumber=$"PERF-{i}",Status=i%2==0?"DELIVERED":"FAILED",CreatedAt=new(2026,11,15,1,0,0,DateTimeKind.Utc),PickupSecureCode="123456",ActualDepartureTime=new(2026,11,15,2,0,0,DateTimeKind.Utc),ActualDeliveryTime=new(2026,11,15,2,20,0,DateTimeKind.Utc)});
            await db.SaveChangesAsync();
        });
        var watch = Stopwatch.StartNew();
        using var response = await client.GetAsync(AnalyticsApiTests.Dashboard + AnalyticsApiTests.Query);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(); watch.Stop();
        Assert.Equal(HttpStatusCode.OK,response.StatusCode); Assert.Equal(5000,body.GetProperty("totalOrders").GetInt32());
        Assert.Equal(50,body.GetProperty("successRate").GetProperty("percent").GetDecimal());
        Assert.Equal(20,body.GetProperty("averageDelivery").GetProperty("minutes").GetDecimal());
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(3),$"Local 5000-order response took {watch.Elapsed.TotalMilliseconds:F0}ms; target <3000ms.");
    }

    private sealed class PauseStatusCounts : DbCommandInterceptor
    {
        public TaskCompletionSource Reached {get;} = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release {get;} = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command,CommandExecutedEventData eventData,DbDataReader result,CancellationToken cancellationToken=default)
        {
            if (command.CommandText.Contains("GROUP BY") && command.CommandText.Contains(".\"Status\"")) {
                Reached.TrySetResult(); await Release.Task.WaitAsync(TimeSpan.FromSeconds(20),cancellationToken);
            }
            return result;
        }
    }
}

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Data.Common;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SmartDroneDelivery.Api.Controllers;
using SmartDroneDelivery.Api.Data;
using SmartDroneDelivery.Api.DTOs;
using SmartDroneDelivery.Api.Entities;
using SmartDroneDelivery.Api.Services;
using SmartDroneDelivery.Api.Security;

namespace SmartDroneDelivery.Tests;

public class IntegrationPostgresTests
{
    [PostgresFact]
    public async Task Migration_hash_storage_creation_ownership_and_revocation_work_on_real_database()
    {
        using var f=new PostgresApiFactory(); using var admin=f.CreateClient();
        var customer=await IntegrationApiTests.ActorAsync(f.Services,admin,"CUSTOMER");
        var body=await IntegrationApiTests.ResourcesAsync(f.Services,customer);
        await IntegrationApiTests.ActorAsync(f.Services,admin,"ADMIN");
        var key=await IntegrationApiTests.ProvisionAsync(admin,customer,TimeProvider.System);
        await f.WithDbAsync(async db=>{
            var persisted=await db.IntegrationApiKeys.SingleAsync(k=>k.Id==key.Id);
            Assert.Equal(64,persisted.KeyHash.Length); Assert.NotEqual(key.Secret,persisted.KeyHash);
            Assert.Equal(IntegrationKeyService.Hash(key.Secret),persisted.KeyHash);
            Assert.DoesNotContain(key.Secret,JsonSerializer.Serialize(persisted));
        });
        using var external=f.CreateClient(); external.DefaultRequestHeaders.Add("X-API-Key",key.Secret);
        var order=await IntegrationApiTests.AssertCreateReadAsync(external,body);
        using var revoked=await admin.DeleteAsync(IntegrationApiTests.Keys+$"/{key.Id}"); Assert.Equal(HttpStatusCode.NoContent,revoked.StatusCode);
        using var denied=await external.GetAsync(IntegrationApiTests.Orders+$"/{order}"); Assert.Equal(HttpStatusCode.Unauthorized,denied.StatusCode);
        await f.WithDbAsync(async db=>{
            Assert.Single(await db.DeliveryOrders.ToListAsync()); Assert.Single(await db.DeliveryStatusLogs.ToListAsync()); Assert.Single(await db.Notifications.ToListAsync());
            Assert.Equal(2,await db.AuditLogs.CountAsync(a=>a.EntityName=="IntegrationApiKey"));
        });
    }

    [PostgresFact]
    public async Task Audit_failure_rolls_back_key_creation_and_revocation()
    {
        using var f=new PostgresApiFactory(); using var admin=f.CreateClient();
        var customer=await IntegrationApiTests.ActorAsync(f.Services,admin,"CUSTOMER");
        var actor=await IntegrationApiTests.ActorAsync(f.Services,admin,"ADMIN");
        await f.WithDbAsync(async db=>{
            await db.Database.ExecuteSqlRawAsync("""
                CREATE FUNCTION reject_key_audit() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN IF NEW."Action" LIKE 'INTEGRATION_KEY_%' THEN RAISE EXCEPTION 'injected key audit failure'; END IF; RETURN NEW; END $$;
                CREATE TRIGGER reject_key_audit BEFORE INSERT ON audit_logs FOR EACH ROW EXECUTE FUNCTION reject_key_audit();
                """);
        });
        await using(var db=Context(f)) {
            await Assert.ThrowsAsync<DbUpdateException>(()=>new IntegrationKeyService(db,TimeProvider.System).CreateAsync(actor,new(customer,"Rollback",["orders:read"],DateTime.UtcNow.AddDays(1))));
        }
        await f.WithDbAsync(async db=>{Assert.Equal(0,await db.IntegrationApiKeys.CountAsync()); Assert.Equal(0,await db.AuditLogs.CountAsync()); await db.Database.ExecuteSqlRawAsync("ALTER TABLE audit_logs DISABLE TRIGGER reject_key_audit");});
        var key=await IntegrationApiTests.ProvisionAsync(admin,customer,TimeProvider.System);
        await f.WithDbAsync(db=>db.Database.ExecuteSqlRawAsync("ALTER TABLE audit_logs ENABLE TRIGGER reject_key_audit"));
        await using(var db=Context(f)) {
            await Assert.ThrowsAsync<DbUpdateException>(()=>new IntegrationKeyService(db,TimeProvider.System).RevokeAsync(actor,key.Id));
        }
        await f.WithDbAsync(async db=>{Assert.Null((await db.IntegrationApiKeys.SingleAsync()).RevokedAt); Assert.Equal(1,await db.AuditLogs.CountAsync());});
        using var ext=f.CreateClient(); ext.DefaultRequestHeaders.Add("X-API-Key",key.Secret);
        using var valid=await ext.GetAsync(IntegrationApiTests.Orders+$"/{Guid.NewGuid()}"); Assert.Equal(HttpStatusCode.NotFound,valid.StatusCode);
    }

    [PostgresFact]
    public async Task Concurrent_external_creates_for_one_package_complete_only_once()
    {
        using var f=new PostgresApiFactory(); using var admin=f.CreateClient();
        var customer=await IntegrationApiTests.ActorAsync(f.Services,admin,"CUSTOMER");
        var body=await IntegrationApiTests.ResourcesAsync(f.Services,customer);
        await IntegrationApiTests.ActorAsync(f.Services,admin,"ADMIN"); var key=await IntegrationApiTests.ProvisionAsync(admin,customer,TimeProvider.System);
        using var a=f.CreateClient(); using var b=f.CreateClient();
        a.DefaultRequestHeaders.Add("X-API-Key",key.Secret); b.DefaultRequestHeaders.Add("X-API-Key",key.Secret);
        var replies=await Task.WhenAll(a.PostAsJsonAsync(IntegrationApiTests.Orders,body),b.PostAsJsonAsync(IntegrationApiTests.Orders,body));
        try {Assert.Single(replies.Where(r=>r.StatusCode==HttpStatusCode.Created)); Assert.Single(replies.Where(r=>r.StatusCode==HttpStatusCode.Conflict));}
        finally {foreach(var r in replies) r.Dispose();}
        await f.WithDbAsync(async db=>{Assert.Equal(1,await db.DeliveryOrders.CountAsync()); Assert.Equal(1,await db.DeliveryStatusLogs.CountAsync()); Assert.Equal(1,await db.Notifications.CountAsync());});
    }

    [PostgresFact]
    public async Task External_tracking_uses_current_mission_and_never_reuses_completed_flight_position()
    {
        using var f=new PostgresApiFactory(); using var admin=f.CreateClient();
        var customer=await IntegrationApiTests.ActorAsync(f.Services,admin,"CUSTOMER"); var body=await IntegrationApiTests.ResourcesAsync(f.Services,customer);
        await IntegrationApiTests.ActorAsync(f.Services,admin,"ADMIN"); var key=await IntegrationApiTests.ProvisionAsync(admin,customer,TimeProvider.System);
        using var ext=f.CreateClient(); ext.DefaultRequestHeaders.Add("X-API-Key",key.Secret);
        var order=await IntegrationApiTests.AssertCreateReadAsync(ext,body);
        Guid mission=default;
        await f.WithDbAsync(async db=>{
            (await db.DeliveryOrders.SingleAsync()).Status="IN_TRANSIT";
            var old=new FlightMission {OrderId=order,FlightStatus="COMPLETED",CreatedAt=DateTime.UtcNow.AddHours(-1),Drone=new Drone {SerialNumber="OLD",ModelName="test"}};
            old.TelemetryLogs.Add(new FlightTelemetryLog {Latitude=10,Longitude=106,BatteryPercentage=80,Timestamp=DateTime.UtcNow});
            var current=new FlightMission {OrderId=order,FlightStatus="IN_AIR",Drone=new Drone {SerialNumber="NEW",ModelName="test"}};
            db.FlightMissions.AddRange(old,current); mission=current.Id; await db.SaveChangesAsync();
        });
        var snapshot=await SnapshotAsync(ext,order); Assert.Equal(JsonValueKind.Null,snapshot.GetProperty("lastPosition").ValueKind); Assert.True(snapshot.GetProperty("telemetryStale").GetBoolean());
        await f.WithDbAsync(async db=>{db.FlightTelemetryLogs.Add(new FlightTelemetryLog {MissionId=mission,Latitude=11,Longitude=107,AltitudeMeters=50,SpeedMps=10,BatteryPercentage=71,Timestamp=DateTime.UtcNow.AddMinutes(-2)}); await db.SaveChangesAsync();});
        snapshot=await SnapshotAsync(ext,order); Assert.Equal(11,snapshot.GetProperty("lastPosition").GetProperty("latitude").GetDecimal()); Assert.True(snapshot.GetProperty("telemetryStale").GetBoolean());
        await f.WithDbAsync(async db=>{(await db.DeliveryOrders.SingleAsync()).Status="ARRIVED"; await db.SaveChangesAsync();});
        snapshot=await SnapshotAsync(ext,order); Assert.Equal(JsonValueKind.Null,snapshot.GetProperty("lastPosition").ValueKind); Assert.False(snapshot.GetProperty("telemetryStale").GetBoolean());
    }

    private static AppDbContext Context(PostgresApiFactory f)=>new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(f.DatabaseConnectionString).Options);

    [PostgresFact]
    public async Task Role_promotion_between_key_eligibility_and_tracking_cannot_grant_foreign_order_access()
    {
        using var f=new PostgresApiFactory(); using var admin=f.CreateClient();
        var customer=await IntegrationApiTests.ActorAsync(f.Services,admin,"CUSTOMER");
        var other=await IntegrationApiTests.ActorAsync(f.Services,admin,"CUSTOMER");
        var body=await IntegrationApiTests.ResourcesAsync(f.Services,other);
        await IntegrationApiTests.ActorAsync(f.Services,admin,"ADMIN"); var key=await IntegrationApiTests.ProvisionAsync(admin,customer,TimeProvider.System);
        Guid foreign=default;
        await f.WithDbAsync(async db=>{foreign=(await new DeliveryOrderService(db).CreateAsync(other,new((Guid)body["packageId"]!,(Guid)body["originStationId"]!,(Guid)body["destinationStationId"]!,null,"Other recipient","0901234567"))).Id;});
        var promote=new PromoteAfterEligibility(async()=>await f.WithDbAsync(async db=>{(await db.Users.SingleAsync(u=>u.Id==customer)).RoleId=await db.Roles.Where(r=>r.Code=="ADMIN").Select(r=>r.Id).SingleAsync(); await db.SaveChangesAsync();}));
        await using var queryDb=new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(f.DatabaseConnectionString).AddInterceptors(promote).Options);
        var controller=new ExternalOrdersController(new IntegrationKeyService(queryDb,TimeProvider.System),new DeliveryOrderService(queryDb),new TrackingService(queryDb,TimeProvider.System),queryDb);
        controller.ControllerContext=new ControllerContext {HttpContext=new DefaultHttpContext {User=new ClaimsPrincipal(new ClaimsIdentity(new[]{new Claim(IntegrationKeyAuthenticationHandler.KeyIdClaim,key.Id.ToString())},"test-key"))}};
        var denied=await Assert.ThrowsAsync<ApiException>(()=>controller.Tracking(foreign.ToString(),default));
        Assert.Contains(denied.StatusCode,new[]{403,404}); Assert.True(promote.Applied);
    }

    private sealed class PromoteAfterEligibility(Func<Task> change):DbCommandInterceptor
    {
        public bool Applied {get;private set;}
        public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command,CommandExecutedEventData eventData,DbDataReader result,CancellationToken cancellationToken=default)
        {
            if(!Applied && command.CommandText.Contains("EXISTS") && command.CommandText.Contains("FROM users") && command.CommandText.Contains("'CUSTOMER'")) {Applied=true; await change();}
            return result;
        }
    }
    private static async Task<JsonElement> SnapshotAsync(HttpClient c,Guid id) {using var r=await c.GetAsync(IntegrationApiTests.Orders+$"/{id}/tracking"); Assert.Equal(HttpStatusCode.OK,r.StatusCode); return await r.Content.ReadFromJsonAsync<JsonElement>();}
}

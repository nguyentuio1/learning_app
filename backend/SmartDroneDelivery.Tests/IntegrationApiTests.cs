using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartDroneDelivery.Api.Data;
using SmartDroneDelivery.Api.Entities;
using SmartDroneDelivery.Api.Services;

namespace SmartDroneDelivery.Tests;

public class IntegrationApiTests
{
    internal const string Keys = "/api/admin/integration-keys";
    internal const string Orders = "/api/v1/delivery-orders";

    [Theory]
    [InlineData("CUSTOMER")][InlineData("MANAGER")][InlineData("DISPATCHER")][InlineData("STATION_OPERATOR")]
    public async Task Nonadmin_cannot_provision_keys(string role)
    {
        using var f = new IntegrationApiFactory(); using var c = f.CreateClient();
        var user = await ActorAsync(f.Services,c,role);
        using var r = await c.PostAsJsonAsync(Keys,KeyBody(user,f.Clock)); Assert.Equal(HttpStatusCode.Forbidden,r.StatusCode);
    }

    [Fact]
    public async Task Secret_is_shown_once_metadata_is_filtered_and_revoke_is_immediate_idempotent()
    {
        using var f = new IntegrationApiFactory(); using var c = f.CreateClient();
        var customer = await ActorAsync(f.Services,c,"CUSTOMER"); await ActorAsync(f.Services,c,"ADMIN");
        var key = await ProvisionAsync(c,customer,f.Clock);
        using var list = await c.GetAsync(Keys+$"?customerId={customer}");
        var rows = await list.Content.ReadFromJsonAsync<JsonElement>(); Assert.Single(rows.EnumerateArray());
        Assert.False(rows[0].TryGetProperty("apiKey",out _)); Assert.False(rows[0].TryGetProperty("keyHash",out _));
        using var empty = await c.GetAsync(Keys+$"?customerId={Guid.NewGuid()}"); Assert.Empty((await empty.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray());
        using var ext = f.CreateClient(); ext.DefaultRequestHeaders.Add("X-API-Key",key.Secret);
        using var missing = await ext.GetAsync(Orders+$"/{Guid.NewGuid()}"); Assert.Equal(HttpStatusCode.NotFound,missing.StatusCode);
        using var revoked = await c.DeleteAsync(Keys+$"/{key.Id}"); Assert.Equal(HttpStatusCode.NoContent,revoked.StatusCode);
        using var repeated = await c.DeleteAsync(Keys+$"/{key.Id}"); Assert.Equal(HttpStatusCode.NoContent,repeated.StatusCode);
        using var denied = await ext.GetAsync(Orders+$"/{Guid.NewGuid()}"); Assert.Equal(HttpStatusCode.Unauthorized,denied.StatusCode);
        await f.WithDbAsync(async db => {
            Assert.Equal(1,await db.AuditLogs.CountAsync(x=>x.Action=="INTEGRATION_KEY_CREATE"));
            Assert.Equal(1,await db.AuditLogs.CountAsync(x=>x.Action=="INTEGRATION_KEY_REVOKE"));
            Assert.DoesNotContain(key.Secret,JsonSerializer.Serialize(await db.AuditLogs.ToListAsync()));
        });
    }

    [Theory]
    [InlineData("unknown-scope")][InlineData("duplicate-scope")][InlineData("empty-scope")]
    [InlineData("expired")][InlineData("unknown-field")][InlineData("blank-name")][InlineData("long-name")]
    public async Task Invalid_provisioning_is_rejected(string kind)
    {
        using var f = new IntegrationApiFactory(); using var c = f.CreateClient();
        var customer = await ActorAsync(f.Services,c,"CUSTOMER"); await ActorAsync(f.Services,c,"ADMIN");
        var body = KeyBody(customer,f.Clock);
        if(kind=="unknown-scope") body["scopes"]=new[]{"admin"};
        if(kind=="duplicate-scope") body["scopes"]=new[]{"orders:read","orders:read"};
        if(kind=="empty-scope") body["scopes"]=Array.Empty<string>();
        if(kind=="expired") body["expiresAt"]=f.Clock.GetUtcNow().UtcDateTime.AddSeconds(-1);
        if(kind=="unknown-field") body["role"]="ADMIN";
        if(kind=="blank-name") body["name"]="  ";
        if(kind=="long-name") body["name"]=new string('a',101);
        using var r = await c.PostAsJsonAsync(Keys,body); Assert.Equal(HttpStatusCode.BadRequest,r.StatusCode);
    }

    [Theory]
    [InlineData("expiry")][InlineData("inactive")][InlineData("role")]
    public async Task Issued_key_rechecks_expiry_and_bound_customer_eligibility(string change)
    {
        using var f = new IntegrationApiFactory(); using var c = f.CreateClient();
        var customer = await ActorAsync(f.Services,c,"CUSTOMER"); await ActorAsync(f.Services,c,"ADMIN");
        var key = await ProvisionAsync(c,customer,f.Clock);
        if(change=="expiry") f.Clock.Advance(TimeSpan.FromDays(2));
        else await f.WithDbAsync(async db=>{var u=await db.Users.SingleAsync(x=>x.Id==customer); if(change=="inactive") u.Status="INACTIVE"; else u.RoleId=await db.Roles.Where(x=>x.Code=="ADMIN").Select(x=>x.Id).SingleAsync(); await db.SaveChangesAsync();});
        using var ext=f.CreateClient(); ext.DefaultRequestHeaders.Add("X-API-Key",key.Secret);
        using var r=await ext.GetAsync(Orders+$"/{Guid.NewGuid()}"); Assert.Equal(change=="expiry"?HttpStatusCode.Unauthorized:HttpStatusCode.Forbidden,r.StatusCode);
    }

    [Fact]
    public async Task External_client_creates_then_reads_and_tracks_own_order_without_revealing_code()
    {
        using var f=new IntegrationApiFactory(); using var c=f.CreateClient();
        var user=await ActorAsync(f.Services,c,"CUSTOMER"); var data=await ResourcesAsync(f.Services,user);
        await ActorAsync(f.Services,c,"ADMIN"); var key=await ProvisionAsync(c,user,f.Clock);
        using var ext=f.CreateClient(); ext.DefaultRequestHeaders.Add("X-API-Key",key.Secret);
        await AssertCreateReadAsync(ext,data);
    }

    internal static async Task<Guid> AssertCreateReadAsync(HttpClient ext,object body)
    {
        using var created=await ext.PostAsJsonAsync(Orders,body); Assert.Equal(HttpStatusCode.Created,created.StatusCode);
        var result=await created.Content.ReadFromJsonAsync<JsonElement>(); var id=result.GetProperty("id").GetGuid();
        Assert.Equal(5,result.EnumerateObject().Count()); Assert.Equal(6,result.GetProperty("pickupSecureCode").GetString()!.Length);
        Assert.Equal(Orders+$"/{id}",created.Headers.Location!.OriginalString);
        using var read=await ext.GetAsync(Orders+$"/{id}"); Assert.Equal(HttpStatusCode.OK,read.StatusCode);
        var order=await read.Content.ReadFromJsonAsync<JsonElement>(); Assert.Equal(id,order.GetProperty("id").GetGuid());
        Assert.Equal(10,order.EnumerateObject().Count()); Assert.False(order.TryGetProperty("pickupSecureCode",out _)); Assert.False(order.TryGetProperty("recipientName",out _));
        using var tracking=await ext.GetAsync(Orders+$"/{id}/tracking"); Assert.Equal(HttpStatusCode.OK,tracking.StatusCode);
        var snapshot=await tracking.Content.ReadFromJsonAsync<JsonElement>(); Assert.Equal(id,snapshot.GetProperty("orderId").GetGuid());
        Assert.Equal(JsonValueKind.Null,snapshot.GetProperty("lastPosition").ValueKind); Assert.False(snapshot.GetProperty("telemetryStale").GetBoolean());
        using var duplicate=await ext.PostAsJsonAsync(Orders,body); Assert.Equal(HttpStatusCode.Conflict,duplicate.StatusCode);
        return id;
    }

    [Fact]
    public async Task Scope_ownership_unknown_fields_and_firstparty_credential_isolation_are_enforced()
    {
        using var f=new IntegrationApiFactory(); using var admin=f.CreateClient();
        var user=await ActorAsync(f.Services,admin,"CUSTOMER"); var body=await ResourcesAsync(f.Services,user);
        var other=await ActorAsync(f.Services,admin,"CUSTOMER"); await ActorAsync(f.Services,admin,"ADMIN");
        var readKey=await ProvisionAsync(admin,user,f.Clock,new[]{"orders:read"});
        using var ext=f.CreateClient(); ext.DefaultRequestHeaders.Add("X-API-Key",readKey.Secret);
        using var scopeDenied=await ext.PostAsJsonAsync(Orders,body); Assert.Equal(HttpStatusCode.Forbidden,scopeDenied.StatusCode);
        var writeKey=await ProvisionAsync(admin,user,f.Clock,new[]{"orders:create"});
        ext.DefaultRequestHeaders.Remove("X-API-Key"); ext.DefaultRequestHeaders.Add("X-API-Key",writeKey.Secret);
        using var cannotRead=await ext.GetAsync(Orders+$"/{Guid.NewGuid()}"); Assert.Equal(HttpStatusCode.Forbidden,cannotRead.StatusCode);
        var key=await ProvisionAsync(admin,user,f.Clock);
        ext.DefaultRequestHeaders.Remove("X-API-Key"); ext.DefaultRequestHeaders.Add("X-API-Key",key.Secret);
        var badBody=new Dictionary<string,object?>(body) { ["customerId"]=other };
        using var overrideIdentity=await ext.PostAsJsonAsync(Orders,badBody); Assert.Equal(HttpStatusCode.BadRequest,overrideIdentity.StatusCode);
        var id=await AssertCreateReadAsync(ext,body);
        var foreignKey=await ProvisionAsync(admin,other,f.Clock);
        ext.DefaultRequestHeaders.Remove("X-API-Key"); ext.DefaultRequestHeaders.Add("X-API-Key",foreignKey.Secret);
        using var foreignOrder=await ext.GetAsync(Orders+$"/{id}"); Assert.Equal(HttpStatusCode.NotFound,foreignOrder.StatusCode);
        using var foreignTracking=await ext.GetAsync(Orders+$"/{id}/tracking"); Assert.Equal(HttpStatusCode.NotFound,foreignTracking.StatusCode);
        using var foreignPackage=await ext.PostAsJsonAsync(Orders,body); Assert.Equal(HttpStatusCode.NotFound,foreignPackage.StatusCode);
        using var firstParty=await ext.GetAsync("/api/delivery-orders"); Assert.Equal(HttpStatusCode.Unauthorized,firstParty.StatusCode);
        ext.DefaultRequestHeaders.Authorization=admin.DefaultRequestHeaders.Authorization;
        using var mixed=await ext.GetAsync(Orders+$"/{id}"); Assert.Equal(HttpStatusCode.BadRequest,mixed.StatusCode);
        using var bearerOnly=await admin.GetAsync(Orders+$"/{id}"); Assert.Equal(HttpStatusCode.Unauthorized,bearerOnly.StatusCode);
    }

    [Theory]
    [InlineData(false,60,Orders)][InlineData(true,10,Orders)][InlineData(true,10,"/API/V1/Delivery-Orders/")]
    public async Task Rate_limit_is_per_key_with_retry_after(bool create,int limit,string route)
    {
        using var f=new IntegrationApiFactory(); using var admin=f.CreateClient();
        var customer=await ActorAsync(f.Services,admin,"CUSTOMER"); await ActorAsync(f.Services,admin,"ADMIN");
        var key=await ProvisionAsync(admin,customer,f.Clock);
        using var ext=f.CreateClient(); ext.DefaultRequestHeaders.Add("X-API-Key",key.Secret);
        for(var i=0;i<limit;i++) { using var r=create?await ext.PostAsJsonAsync(route,new{}):await ext.GetAsync(route+$"/{Guid.NewGuid()}"); Assert.NotEqual(HttpStatusCode.TooManyRequests,r.StatusCode); }
        using var blocked=create?await ext.PostAsJsonAsync(route,new{}):await ext.GetAsync(route+$"/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.TooManyRequests,blocked.StatusCode); Assert.True(blocked.Headers.RetryAfter!.Delta>TimeSpan.Zero);
        var fresh=await ProvisionAsync(admin,customer,f.Clock);
        ext.DefaultRequestHeaders.Remove("X-API-Key"); ext.DefaultRequestHeaders.Add("X-API-Key",fresh.Secret);
        using var isolated=create?await ext.PostAsJsonAsync(Orders,new{}):await ext.GetAsync(Orders+$"/{Guid.NewGuid()}"); Assert.NotEqual(HttpStatusCode.TooManyRequests,isolated.StatusCode);
    }

    [Fact]
    public async Task Swagger_declares_external_API_key_only_and_admin_bearer_routes()
    {
        using var f=new IntegrationApiFactory(); using var c=f.CreateClient();
        var doc=await c.GetFromJsonAsync<JsonElement>("/swagger/v1/swagger.json");
        Assert.Equal("X-API-Key",doc.GetProperty("components").GetProperty("securitySchemes").GetProperty("IntegrationKey").GetProperty("name").GetString());
        var paths=doc.GetProperty("paths"); Assert.True(paths.GetProperty(Orders).GetProperty("post").GetProperty("security")[0].TryGetProperty("IntegrationKey",out _));
        Assert.True(paths.GetProperty(Keys).GetProperty("post").GetProperty("security")[0].TryGetProperty("Bearer",out _));
        Assert.True(paths.GetProperty(Orders+"/{id}/tracking").TryGetProperty("get",out _));
    }

    [Theory]
    [InlineData(true)][InlineData(false)]
    public async Task Read_and_create_budgets_stay_separate_regardless_of_first_request(bool readFirst)
    {
        using var f=new IntegrationApiFactory(); using var admin=f.CreateClient();
        var customer=await ActorAsync(f.Services,admin,"CUSTOMER"); await ActorAsync(f.Services,admin,"ADMIN");
        var key=await ProvisionAsync(admin,customer,f.Clock);
        using var ext=f.CreateClient(); ext.DefaultRequestHeaders.Add("X-API-Key",key.Secret);
        if(readFirst) {using var first=await ext.GetAsync(Orders+$"/{Guid.NewGuid()}"); Assert.Equal(HttpStatusCode.NotFound,first.StatusCode);}
        else {
            using var first=await ext.PostAsJsonAsync(Orders,new{}); Assert.Equal(HttpStatusCode.BadRequest,first.StatusCode);
            for(var i=0;i<15;i++) {using var read=await ext.GetAsync(Orders+$"/{Guid.NewGuid()}"); Assert.Equal(HttpStatusCode.NotFound,read.StatusCode);}
        }
        for(var i=0;i<(readFirst?10:9);i++) {using var create=await ext.PostAsJsonAsync(Orders,new{}); Assert.Equal(HttpStatusCode.BadRequest,create.StatusCode);}
        using var blocked=await ext.PostAsJsonAsync(Orders,new{}); Assert.Equal(HttpStatusCode.TooManyRequests,blocked.StatusCode);
    }

    internal static Dictionary<string,object?> KeyBody(Guid customer,TimeProvider clock,string[]? scopes=null)=>new(){["customerId"]=customer,["name"]="Warehouse A",["scopes"]=scopes??new[]{"orders:create","orders:read"},["expiresAt"]=clock.GetUtcNow().UtcDateTime.AddDays(1)};

    [Fact]
    public async Task Demoted_admin_cannot_manage_keys_and_ineligible_customer_cannot_receive_key()
    {
        using var f=new IntegrationApiFactory(); using var c=f.CreateClient();
        var customer=await ActorAsync(f.Services,c,"CUSTOMER"); var admin=await ActorAsync(f.Services,c,"ADMIN");
        using var invalidTarget=await c.PostAsJsonAsync(Keys,KeyBody(admin,f.Clock)); Assert.Equal(HttpStatusCode.Conflict,invalidTarget.StatusCode);
        await f.WithDbAsync(async db=>{(await db.Users.SingleAsync(u=>u.Id==admin)).RoleId=await db.Roles.Where(r=>r.Code=="CUSTOMER").Select(r=>r.Id).SingleAsync(); await db.SaveChangesAsync();});
        using var denied=await c.PostAsJsonAsync(Keys,KeyBody(customer,f.Clock)); Assert.Equal(HttpStatusCode.Forbidden,denied.StatusCode);
        using var list=await c.GetAsync(Keys); Assert.Equal(HttpStatusCode.Forbidden,list.StatusCode);
        using var revoke=await c.DeleteAsync(Keys+$"/{Guid.NewGuid()}"); Assert.Equal(HttpStatusCode.Forbidden,revoke.StatusCode);
    }

    [Fact]
    public async Task Foreign_address_is_not_accepted_for_an_owned_package()
    {
        using var f=new IntegrationApiFactory(); using var admin=f.CreateClient();
        var customer=await ActorAsync(f.Services,admin,"CUSTOMER"); var body=await ResourcesAsync(f.Services,customer);
        var other=await ActorAsync(f.Services,admin,"CUSTOMER"); var address=new CustomerAddress {UserId=other,Label="Other",ContactName="Other",ContactPhone="0901234567",AddressLine="Other street"};
        await f.WithDbAsync(async db=>{db.CustomerAddresses.Add(address); await db.SaveChangesAsync();});
        body["deliveryAddressId"]=address.Id; await ActorAsync(f.Services,admin,"ADMIN"); var key=await ProvisionAsync(admin,customer,f.Clock);
        using var ext=f.CreateClient(); ext.DefaultRequestHeaders.Add("X-API-Key",key.Secret);
        using var denied=await ext.PostAsJsonAsync(Orders,body); Assert.Equal(HttpStatusCode.NotFound,denied.StatusCode);
        await f.WithDbAsync(async db=>Assert.Equal(0,await db.DeliveryOrders.CountAsync()));
    }
    internal static async Task<(Guid Id,string Secret)> ProvisionAsync(HttpClient admin,Guid customer,TimeProvider clock,string[]? scopes=null)
    {
        using var r=await admin.PostAsJsonAsync(Keys,KeyBody(customer,clock,scopes)); Assert.Equal(HttpStatusCode.Created,r.StatusCode);
        var key=await r.Content.ReadFromJsonAsync<JsonElement>(); return(key.GetProperty("id").GetGuid(),key.GetProperty("apiKey").GetString()!);
    }
    internal static async Task<Guid> ActorAsync(IServiceProvider services,HttpClient client,string role)
    {
        using var scope=services.CreateScope(); var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user=new User {Email=$"int-{Guid.NewGuid():N}@test.com",FullName="Integration",PhoneNumber=Guid.NewGuid().ToString("N")[..15],PasswordHash="unused",Status="ACTIVE",Role=await db.Roles.SingleAsync(r=>r.Code==role)};
        db.Users.Add(user); await db.SaveChangesAsync();
        client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",scope.ServiceProvider.GetRequiredService<ITokenService>().CreateAccessToken(user,DateTime.UtcNow)); return user.Id;
    }
    internal static async Task<Dictionary<string,object?>> ResourcesAsync(IServiceProvider services,Guid customer)
    {
        using var scope=services.CreateScope(); var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var a=new LandingStation {StationCode=$"A-{Guid.NewGuid():N}",Name="Origin",Address="A"};
        var b=new LandingStation {StationCode=$"B-{Guid.NewGuid():N}",Name="Destination",Address="B"};
        var p=new Package {SenderId=customer,PackageName="External",WeightGram=100,LengthCm=10,WidthCm=10,HeightCm=10};
        db.LandingStations.AddRange(a,b); db.Packages.Add(p); await db.SaveChangesAsync();
        return new(){["packageId"]=p.Id,["originStationId"]=a.Id,["destinationStationId"]=b.Id,["recipientName"]="Test Recipient",["recipientPhone"]="0901234567",["deliveryAddressId"]=null};
    }
}

internal sealed class IntegrationApiFactory:AuthApiFactory
{
    public IntegrationClock Clock {get;}=new();
    protected override void ConfigureWebHost(IWebHostBuilder builder) {base.ConfigureWebHost(builder); builder.ConfigureServices(s=>s.AddSingleton<TimeProvider>(Clock));}
}
internal sealed class IntegrationClock:TimeProvider
{
    private DateTimeOffset now=DateTimeOffset.UtcNow;
    public override DateTimeOffset GetUtcNow()=>now;
    public void Advance(TimeSpan value)=>now+=value;
}

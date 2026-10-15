using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SmartDroneDelivery.Api.Data;
using SmartDroneDelivery.Api.Services;

namespace SmartDroneDelivery.Tests;

/// <summary>Runs the real API in memory with an in-memory database instead of PostgreSQL.</summary>
public class AuthApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = $"auth-tests-{Guid.NewGuid()}";

    /// <summary>Replaces MinIO so the tests do not need a running storage server.</summary>
    public FakeAvatarStorage Avatars { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        // Test-only controller with one endpoint per role rule.
        builder.ConfigureServices(services => services.AddControllers().AddApplicationPart(typeof(RbacProbeController).Assembly));
        builder.ConfigureServices(services =>
        {
            foreach (var descriptor in services
                         .Where(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>)
                                     || d.ServiceType == typeof(DbContextOptions)).ToList())
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(_dbName));

            services.RemoveAll<IAvatarStorage>();
            services.AddSingleton<IAvatarStorage>(Avatars);
        });
    }

    public async Task WithDbAsync(Func<AppDbContext, Task> action)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await action(db);
    }
}

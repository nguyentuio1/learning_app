using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SmartDroneDelivery.Api.Data;
using SmartDroneDelivery.Api.Services;

namespace SmartDroneDelivery.Tests;

public class AuthApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = $"auth-tests-{Guid.NewGuid()}";
    public FakeAvatarStorage Avatars { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

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

            services.AddHostedService<TestLifetimeInterceptor>();
        });
    }

    public async Task WithDbAsync(Func<AppDbContext, Task> action)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await action(db);
    }
}

public class TestLifetimeInterceptor : Microsoft.Extensions.Hosting.IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

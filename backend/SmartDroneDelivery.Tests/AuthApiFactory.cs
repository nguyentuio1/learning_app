using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using SmartDroneDelivery.Api.Data;
using SmartDroneDelivery.Api.Services;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SmartDroneDelivery.Tests;

/// <summary>Runs the real API in memory with an in-memory database instead of PostgreSQL.</summary>
public class AuthApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = $"auth-tests-{Guid.NewGuid()}";

    /// <summary>Replaces MinIO so the tests do not need a running storage server.</summary>
    public FakeAvatarStorage Avatars { get; } = new();

    // 🛠️ GIẢI PHÁP ĐÁNH CHẶN CẤP CAO: 
    // Ghi đè phương thức khởi tạo Host gốc để cấu hình môi trường bảo vệ TRƯỚC khi file Program.cs chạy logic thật.
    protected override IHost CreateHost(IHostBuilder builder)
    {
        // Ép toàn bộ pipeline nhận diện môi trường độc lập "Testing" để tránh kích hoạt app.Run()
        builder.UseEnvironment("Testing");

        return base.CreateHost(builder);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

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

            // Đăng ký bộ liên kết đánh chặn giả lập vòng đời của IHost
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

// =========================================================================
// Lớp hỗ trợ đánh chặn luồng chạy của Host (Bảo vệ ứng dụng khỏi bị sập)
// =========================================================================
public class TestLifetimeInterceptor : Microsoft.Extensions.Hosting.IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

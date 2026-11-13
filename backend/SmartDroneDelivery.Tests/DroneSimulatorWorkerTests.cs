using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmartDroneDelivery.Api.Data;
using SmartDroneDelivery.Api.DTOs;
using SmartDroneDelivery.Api.Entities;
using SmartDroneDelivery.Api.Options;
using SmartDroneDelivery.Api.Services;

namespace SmartDroneDelivery.Tests;

public class DroneSimulatorWorkerTests
{
    [Fact]
    public async Task Worker_uses_scopes_retries_failed_tick_without_logging_exception_content_and_stops_on_cancellation()
    {
        using var baseFactory = new SimulatorApiFactory();
        var clock = new ManualTimerClock();
        var attempts = new Attempts();
        using var factory = baseFactory.WithWebHostBuilder(builder => builder.ConfigureServices(services => {
            services.RemoveAll<TimeProvider>(); services.AddSingleton<TimeProvider>(clock);
            services.RemoveAll<IFlightTelemetryService>();
            services.AddScoped<IFlightTelemetryService>(scope => new RejectFirstTelemetry(
                new FlightTelemetryService(scope.GetRequiredService<AppDbContext>(), clock), attempts));
        }));
        _ = factory.CreateClient();
        Guid id = default;
        using (var scope = factory.Services.CreateScope())
            id = await SimulatorData.SeedAsync(scope.ServiceProvider.GetRequiredService<AppDbContext>(), clock.Now);
        var logger = new WorkerLogger();
        using var worker = new DroneSimulatorWorker(factory.Services.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new DroneSimulatorOptions { Enabled = true }), clock, logger);
        await worker.StartAsync(CancellationToken.None);
        await clock.TimerReady.Task.WaitAsync(TimeSpan.FromSeconds(5));
        clock.Fire();
        await logger.Error.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("Drone simulator tick failed; retrying at the next interval.", logger.Message);
        Assert.Null(logger.Exception);
        clock.Fire();
        await attempts.Succeeded.Task.WaitAsync(TimeSpan.FromSeconds(5));
        // Staging is deliberately before persistence; wait for the real save before
        // requesting shutdown, otherwise cancellation correctly rolls the tick back.
        for (var i = 0; i < 100; i++) {
            using var observing = factory.Services.CreateScope();
            var db = observing.ServiceProvider.GetRequiredService<AppDbContext>();
            if (await db.FlightTelemetryLogs.AnyAsync(x => x.MissionId == id)) break;
            await Task.Delay(10);
        }
        await worker.StopAsync(CancellationToken.None);
        using (var scope = factory.Services.CreateScope()) {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Equal("IN_AIR", (await db.FlightMissions.FindAsync(id))!.FlightStatus);
            Assert.Equal(1, await db.FlightTelemetryLogs.CountAsync(x => x.MissionId == id));
        }
        Assert.True(clock.TimerDisposed);
        clock.Fire(); Assert.Equal(2, attempts.Count);
    }

    private sealed class Attempts
    {
        public int Count;
        public TaskCompletionSource Succeeded { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
    private sealed class RejectFirstTelemetry(IFlightTelemetryService inner, Attempts attempts) : IFlightTelemetryService
    {
        public void Stage(FlightMission mission, TelemetrySampleRequest request)
        {
            if (Interlocked.Increment(ref attempts.Count) == 1) throw new InvalidOperationException("private provider message");
            inner.Stage(mission, request); attempts.Succeeded.TrySetResult();
        }
        public Task IngestAsync(Guid id, TelemetrySampleRequest request, CancellationToken ct = default) => inner.IngestAsync(id, request, ct);
        public Task IngestForAdminAsync(Guid userId, Guid id, TelemetrySampleRequest request, CancellationToken ct = default) => inner.IngestForAdminAsync(userId, id, request, ct);
        public Task<MissionTelemetrySnapshot> GetLatestAsync(Guid id, CancellationToken ct = default) => inner.GetLatestAsync(id, ct);
    }
    private sealed class WorkerLogger : ILogger<DroneSimulatorWorker>
    {
        public TaskCompletionSource Error { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public string? Message; public Exception? Exception;
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        { Message = formatter(state, exception); Exception = exception; Error.TrySetResult(); }
    }
    private sealed class ManualTimerClock : SimulatorClock
    {
        private ManualTimer? _timer;
        public TaskCompletionSource TimerReady { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool TimerDisposed => _timer!.Disposed;
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        { _timer = new(callback, state); TimerReady.TrySetResult(); return _timer; }
        public void Fire() { Now = Now.AddSeconds(2); _timer!.Fire(); }
        private sealed class ManualTimer(TimerCallback callback, object? state) : ITimer
        {
            public bool Disposed;
            public bool Change(TimeSpan dueTime, TimeSpan period) => !Disposed;
            public void Fire() { if (!Disposed) callback(state); }
            public void Dispose() => Disposed = true;
            public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
        }
    }
}

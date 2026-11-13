using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using SmartDroneDelivery.Api.Options;

namespace SmartDroneDelivery.Tests;

public class DroneSimulatorConfigurationTests
{
    [Fact]
    public async Task Default_configuration_is_disabled_with_shared_speed_and_no_tick_effects()
    {
        using var factory = new AuthApiFactory();
        _ = factory.CreateClient();
        var options = factory.Services.GetRequiredService<IOptions<DroneSimulatorOptions>>().Value;
        Assert.False(options.Enabled); Assert.Equal(2, options.TickIntervalSeconds); Assert.Equal(80, options.AltitudeMeters);
        Assert.Equal(12, factory.Services.GetRequiredService<IOptions<DeliveryFlightOptions>>().Value.CruiseSpeedMps);
        Guid id = default;
        await factory.WithDbAsync(async db => id = await SimulatorData.SeedAsync(db, DateTimeOffset.UtcNow.AddMinutes(-1)));
        await SimulatorData.TickAsync(factory.Services);
        await factory.WithDbAsync(async db => {
            Assert.Equal("PLANNED", (await db.FlightMissions.FindAsync(id))!.FlightStatus);
            Assert.Equal(0, await db.FlightTelemetryLogs.CountAsync(x => x.MissionId == id));
        });
        Assert.Single(factory.Services.GetServices<TimeProvider>());
    }

    [Theory]
    [InlineData("DroneSimulator:TickIntervalSeconds", "0")]
    [InlineData("DroneSimulator:TickIntervalSeconds", "-1")]
    [InlineData("DroneSimulator:TickIntervalSeconds", "NaN")]
    [InlineData("DroneSimulator:TickIntervalSeconds", "Infinity")]
    [InlineData("DroneSimulator:TickIntervalSeconds", "0.0001")]
    [InlineData("DroneSimulator:TickIntervalSeconds", "4294967.295")]
    [InlineData("DroneSimulator:AltitudeMeters", "-0.01")]
    [InlineData("DroneSimulator:AltitudeMeters", "10000")]
    [InlineData("DeliveryFlight:CruiseSpeedMps", "0")]
    [InlineData("DeliveryFlight:CruiseSpeedMps", "NaN")]
    [InlineData("DeliveryFlight:CruiseSpeedMps", "1000")]
    public void Invalid_configuration_is_rejected_on_start_even_if_disabled(string key, string value)
    {
        using var baseFactory = new AuthApiFactory();
        using var factory = baseFactory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?> { [key] = value })));
        Assert.Throws<OptionsValidationException>(() => factory.CreateClient());
    }
}

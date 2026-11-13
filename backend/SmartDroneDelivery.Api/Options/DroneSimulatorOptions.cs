namespace SmartDroneDelivery.Api.Options;

public class DroneSimulatorOptions
{
    public const string SectionName = "DroneSimulator";
    public bool Enabled { get; set; }
    public double TickIntervalSeconds { get; set; } = 2;
    public decimal AltitudeMeters { get; set; } = 80;
}

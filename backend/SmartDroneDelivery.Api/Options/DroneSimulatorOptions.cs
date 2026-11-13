namespace SmartDroneDelivery.Api.Options;

public class DroneSimulatorOptions
{
    public const string SectionName = "DroneSimulator";
    public bool Enabled { get; set; }
    public double TickIntervalSeconds { get; set; } = 2;
    public decimal AltitudeMeters { get; set; } = 80;
    public double? TelemetryLossAfterSeconds { get; set; }
    public double? TelemetryLossDurationSeconds { get; set; }
    public double? AbortAfterSeconds { get; set; }

    public bool HasValidScenarios() =>
        ValidOffset(AbortAfterSeconds) && ValidOffset(TelemetryLossAfterSeconds)
        && TelemetryLossAfterSeconds.HasValue == TelemetryLossDurationSeconds.HasValue
        && (TelemetryLossDurationSeconds is null || double.IsFinite(TelemetryLossDurationSeconds.Value)
            && TelemetryLossDurationSeconds > 0 && TelemetryLossDurationSeconds <= 86400);

    // Bounded demo offsets avoid overflow and accidental multi-year simulations.
    private static bool ValidOffset(double? value) => value is null || double.IsFinite(value.Value) && value >= 0 && value <= 86400;
}

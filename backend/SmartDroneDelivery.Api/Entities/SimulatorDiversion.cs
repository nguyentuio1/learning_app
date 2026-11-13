namespace SmartDroneDelivery.Api.Entities;

/// <summary>Persisted software-only navigation and landing evidence; never parcel receipt.</summary>
public class SimulatorDiversion
{
    public Guid MissionId { get; set; }
    public FlightMission Mission { get; set; } = null!;
    public DateTime AbortedAt { get; set; }
    public decimal AbortLatitude { get; set; }
    public decimal AbortLongitude { get; set; }
    public double CruiseSpeedMps { get; set; }
    public Guid? LandingStationId { get; set; }
    public LandingStation? LandingStation { get; set; }
    public decimal? LandingLatitude { get; set; }
    public decimal? LandingLongitude { get; set; }
    public DateTime? NavigationStartedAt { get; set; }
    public DateTime? LandingDueAt { get; set; }
    public DateTime? LandedAt { get; set; }
}

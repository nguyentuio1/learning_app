namespace SmartDroneDelivery.Api.Options;

/// <summary>LA-125: settings of the job that fails parcels nobody collected (docs/api/station-api.md section 4.6).</summary>
public class PickupTimeoutOptions
{
    public const string SectionName = "PickupTimeout";

    /// <summary>Turns the background job off (for example in a second instance or in a test host).</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>How often the job looks for overdue parcels, in minutes. The first run happens after one interval.</summary>
    public int IntervalMinutes { get; set; } = 15;

    /// <summary>Hours a parcel may stay in <c>ARRIVED</c> before it fails (BR-LS-19).</summary>
    public int WindowHours { get; set; } = 24;
}

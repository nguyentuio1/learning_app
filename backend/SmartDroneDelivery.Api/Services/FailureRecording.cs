using Microsoft.EntityFrameworkCore;
using SmartDroneDelivery.Api.Data;
using SmartDroneDelivery.Api.Entities;

namespace SmartDroneDelivery.Api.Services;

/// <summary>Stages metadata only. Caller owns the operation gate, transaction, notification, audit and save.</summary>
public static class FailureRecording
{
    public static void Record(AppDbContext db, DeliveryStatusLog failedLog, string type, string source)
    {
        if (failedLog.ToStatus != "FAILED" || db.Entry(failedLog).State != EntityState.Added)
            throw new InvalidOperationException("Record requires the newly staged FAILED status log.");
        if (type is not ("MISSION_ABORTED" or "EMERGENCY_LANDING" or "HARDWARE_FAULT" or "COLLECTION_TIMEOUT" or "UNKNOWN"))
            throw new ArgumentException("Unsupported failure classification.", nameof(type));
        db.DeliveryFailureEpisodes.Add(new DeliveryFailureEpisode { FailureLog = failedLog,
            FailureType = type, Source = source, OriginalReason = failedLog.Remarks });
    }
}

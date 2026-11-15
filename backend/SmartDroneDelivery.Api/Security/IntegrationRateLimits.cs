using System.Threading.RateLimiting;

namespace SmartDroneDelivery.Api.Security;

public sealed class IntegrationRateLimits : IDisposable
{
    private readonly PartitionedRateLimiter<(Guid Key,bool Create)> limiter = PartitionedRateLimiter.CreateChained(
        PartitionedRateLimiter.Create<(Guid Key,bool Create),Guid>(r => RateLimitPartition.GetFixedWindowLimiter(r.Key,_ => Options(60))),
        PartitionedRateLimiter.Create<(Guid Key,bool Create),(Guid Key,bool Create)>(r => r.Create
            ? RateLimitPartition.GetFixedWindowLimiter(r,_ => Options(10))
            : RateLimitPartition.GetNoLimiter(r)));
    private static FixedWindowRateLimiterOptions Options(int limit)=>new(){PermitLimit=limit,Window=TimeSpan.FromMinutes(1),QueueLimit=0,AutoReplenishment=true};
    public ValueTask<RateLimitLease> AcquireAsync(Guid key,bool create,CancellationToken ct)=>limiter.AcquireAsync((key,create),1,ct);
    public void Dispose()=>limiter.Dispose();
}

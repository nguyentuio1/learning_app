using System.Collections.Concurrent;

namespace SmartDroneDelivery.Api.Services;

/// <summary>Single-process account concurrency gate. Multi-instance deployment needs a shared gate.</summary>
public class AiGenerationSlots
{
    private readonly ConcurrentDictionary<Guid, byte> active = new();
    public IDisposable? TryAcquire(Guid userId) => active.TryAdd(userId, 0) ? new Lease(this, userId) : null;
    private sealed class Lease(AiGenerationSlots owner, Guid userId) : IDisposable
    {
        private int disposed;
        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) == 0) owner.active.TryRemove(userId, out _);
        }
    }
}

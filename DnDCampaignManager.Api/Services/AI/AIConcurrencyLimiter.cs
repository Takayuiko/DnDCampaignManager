using System.Threading.RateLimiting;

namespace DnDCampaignManager.Api.Services.AI;

public sealed class AIBusyException() : Exception("AI capacity is busy. Please try again shortly.");

// One singleton protects all paid provider operations in this API process.
public sealed class AIConcurrencyLimiter : IDisposable
{
    private readonly ConcurrencyLimiter _limiter;
    private readonly TimeSpan _wait;

    public AIConcurrencyLimiter(IConfiguration configuration)
    {
        var limits = new AIRequestLimits(configuration);
        _wait = TimeSpan.FromSeconds(limits.ConcurrencyWaitSeconds);
        _limiter = new(new ConcurrencyLimiterOptions {
            PermitLimit = limits.MaxConcurrentRequests,
            QueueLimit = limits.MaxQueuedRequests,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst
        });
    }

    public async ValueTask<RateLimitLease> AcquireAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(_wait);
        RateLimitLease lease;
        try { lease = await _limiter.AcquireAsync(1, deadline.Token); }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { throw new AIBusyException(); }
        if (lease.IsAcquired) return lease;
        lease.Dispose();
        cancellationToken.ThrowIfCancellationRequested();
        throw new AIBusyException();
    }

    public void Dispose() => _limiter.Dispose();
}

namespace DnDCampaignManager.Api.Services.AI;

public sealed class AIRequestLimitException(string message) : InvalidOperationException(message);

// Character/byte budgets are explicit application bounds, not tokenizer estimates.
public sealed class AIRequestLimits(IConfiguration configuration)
{
    private int Limit(string name, int fallback, int min, int max) =>
        Math.Clamp(configuration.GetValue($"OpenAI:Limits:{name}", fallback), min, max);
    public int MaxContextCharacters => Limit(nameof(MaxContextCharacters), 48000, 1000, 100000);
    public int MaxHistoryCharacters => Limit(nameof(MaxHistoryCharacters), 24000, 8000, 100000);
    public int MaxRequestBytes => Limit(nameof(MaxRequestBytes), 256000, 16000, 1000000);
    public int MaxOutputTokens => Limit(nameof(MaxOutputTokens), 4096, 256, 32000);
    public int RequestTimeoutSeconds => Limit(nameof(RequestTimeoutSeconds), 120, 1, 600);
    public int EmbeddingTimeoutSeconds => Limit(nameof(EmbeddingTimeoutSeconds), 30, 1, 120);
    public int TranscriptionTimeoutSeconds => Limit(nameof(TranscriptionTimeoutSeconds), 120, 1, 600);
    public int MaxConcurrentRequests => Limit(nameof(MaxConcurrentRequests), 4, 1, 32);
    public int MaxQueuedRequests => Limit(nameof(MaxQueuedRequests), 8, 0, 64);
    public int ConcurrencyWaitSeconds => Limit(nameof(ConcurrencyWaitSeconds), 5, 1, 30);

    public CancellationTokenSource Deadline(CancellationToken ct, int seconds)
    {
        var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(seconds));
        return deadline;
    }
}

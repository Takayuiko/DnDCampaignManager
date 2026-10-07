namespace DnDCampaignManager.Api.Services.AI;

public sealed record EmbeddingBatch(IReadOnlyList<float[]> Vectors, int InputTokens);

public interface IEmbeddingService
{
    string Model { get; }
    int Dimensions { get; }
    Task<EmbeddingBatch> EmbedAsync(IReadOnlyList<string> inputs, CancellationToken cancellationToken);
}

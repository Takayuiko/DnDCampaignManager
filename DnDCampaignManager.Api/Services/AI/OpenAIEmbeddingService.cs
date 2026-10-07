using OpenAI.Embeddings;

namespace DnDCampaignManager.Api.Services.AI;

public sealed class OpenAIEmbeddingService(EmbeddingClient client, IConfiguration configuration, AIConcurrencyLimiter concurrency) : IEmbeddingService
{
    public string Model => configuration["OpenAI:EmbeddingModel"] ?? "text-embedding-3-small";
    public int Dimensions => configuration.GetValue("OpenAI:EmbeddingDimensions", 512);

    public async Task<EmbeddingBatch> EmbedAsync(IReadOnlyList<string> inputs, CancellationToken cancellationToken)
    {
        using var deadline = new AIRequestLimits(configuration).Deadline(cancellationToken, new AIRequestLimits(configuration).EmbeddingTimeoutSeconds);
        cancellationToken = deadline.Token;
        using var permit = await concurrency.AcquireAsync(cancellationToken);
        OpenAIEmbeddingCollection result = await client.GenerateEmbeddingsAsync(inputs,
            new EmbeddingGenerationOptions { Dimensions = Dimensions }, cancellationToken);
        return new EmbeddingBatch(result.OrderBy(x => x.Index).Select(x => x.ToFloats().ToArray()).ToArray(),
            result.Usage.InputTokenCount);
    }
}

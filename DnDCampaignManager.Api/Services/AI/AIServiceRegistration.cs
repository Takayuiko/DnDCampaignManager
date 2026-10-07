using DnDCampaignManager.Api.Models.AI;
using OpenAI.Audio;
using OpenAI.Embeddings;
using OpenAI.Responses;

namespace DnDCampaignManager.Api.Services.AI;

public static class AIServiceRegistration
{
    public const string UnavailableMessage = "AI features are unavailable because OpenAI is not configured on the server.";

    public static IServiceCollection AddCampaignAI(this IServiceCollection services, IConfiguration configuration)
    {
        var key = configuration["OpenAI:ApiKey"];
        if (!string.IsNullOrWhiteSpace(key))
        {
            services.AddSingleton(new ResponsesClient(key));
            services.AddScoped<IAIService, OpenAIService>();
            services.AddSingleton(new EmbeddingClient(configuration["OpenAI:EmbeddingModel"] ?? "text-embedding-3-small", key));
            services.AddScoped<IEmbeddingService, OpenAIEmbeddingService>();
            services.AddSingleton(new AudioClient(configuration["OpenAI:TranscriptionModel"] ?? "gpt-transcribe", key));
            services.AddScoped<IAudioTranscriptionService, OpenAIAudioTranscriptionService>();
            services.AddHostedService<MapIndexingWorker>();
        }
        else
        {
            services.AddScoped<IAIService, UnavailableAIProvider>();
            services.AddScoped<IEmbeddingService, UnavailableAIProvider>();
            services.AddScoped<IAudioTranscriptionService, UnavailableAIProvider>();
        }
        services.AddScoped<CampaignKnowledgeService>();
        services.AddScoped<MapKnowledgeService>();
        services.AddScoped<CampaignToolService>();
        return services;
    }
}

// Fail explicitly if an unavailable provider is called outside the guarded HTTP operations.
internal sealed class UnavailableAIProvider(IConfiguration configuration) : IAIService, IEmbeddingService, IAudioTranscriptionService
{
    public bool IsAvailable => false;
    string IAIService.Model => configuration["OpenAI:Model"] ?? "gpt-5.2";
    string IEmbeddingService.Model => configuration["OpenAI:EmbeddingModel"] ?? "text-embedding-3-small";
    string IAudioTranscriptionService.Model => configuration["OpenAI:TranscriptionModel"] ?? "gpt-transcribe";
    public int Dimensions => configuration.GetValue("OpenAI:EmbeddingDimensions", 512);

    public Task<AICompletionResult> GetChatResponseAsync(IReadOnlyCollection<AIMessage> history,
        CancellationToken cancellationToken = default, string? campaignContext = null, CampaignToolScope? toolScope = null)
        => throw new InvalidOperationException(AIServiceRegistration.UnavailableMessage);

    public IAsyncEnumerable<AIStreamEvent> StreamChatResponseAsync(IReadOnlyCollection<AIMessage> history,
        CancellationToken cancellationToken = default, string? campaignContext = null, CampaignToolScope? toolScope = null)
        => throw new InvalidOperationException(AIServiceRegistration.UnavailableMessage);

    public Task<EmbeddingBatch> EmbedAsync(IReadOnlyList<string> inputs, CancellationToken cancellationToken)
        => throw new InvalidOperationException(AIServiceRegistration.UnavailableMessage);

    public Task<string> TranscribeAsync(Stream audio, string fileName, CancellationToken ct)
        => throw new InvalidOperationException(AIServiceRegistration.UnavailableMessage);
}

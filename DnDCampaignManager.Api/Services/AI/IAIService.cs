using DnDCampaignManager.Api.Models.AI;

namespace DnDCampaignManager.Api.Services.AI;

public interface IAIService
{
    bool IsAvailable => true;
    string Model { get; }

    Task<AICompletionResult> GetChatResponseAsync(
        IReadOnlyCollection<AIMessage> history,
        CancellationToken cancellationToken = default, string? campaignContext = null, CampaignToolScope? toolScope = null);

    IAsyncEnumerable<AIStreamEvent> StreamChatResponseAsync(
        IReadOnlyCollection<AIMessage> history,
        CancellationToken cancellationToken = default, string? campaignContext = null, CampaignToolScope? toolScope = null);
}

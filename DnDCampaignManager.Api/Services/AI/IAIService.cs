using DnDCampaignManager.Api.Models.AI;

namespace DnDCampaignManager.Api.Services.AI;

public interface IAIService
{
    string Model { get; }

    Task<AICompletionResult> GetChatResponseAsync(
        IReadOnlyCollection<AIMessage> history,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<AIStreamEvent> StreamChatResponseAsync(
        IReadOnlyCollection<AIMessage> history,
        CancellationToken cancellationToken = default);
}

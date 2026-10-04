using DnDCampaignManager.Api.DTOs.AI_DTO;

namespace DnDCampaignManager.Api.Services;

public interface IAIService
{
    string Model { get; }

    Task<string> GetChatResponseAsync(
        string userMessage,
        IReadOnlyCollection<ChatMessage> history,
        CancellationToken cancellationToken = default);
}

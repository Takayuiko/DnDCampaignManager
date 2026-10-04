namespace DnDCampaignManager.Api.DTOs.AI_DTO;

public sealed class ChatRequest
{
    public string Message { get; set; } = string.Empty;

    public List<ChatMessage> History { get; set; } = [];
}

public sealed class ChatMessage
{
    public string Role { get; set; } = "user";

    public string Content { get; set; } = string.Empty;
}

public sealed record ChatResponse(
    string Reply,
    string Model);

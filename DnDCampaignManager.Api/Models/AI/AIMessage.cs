namespace DnDCampaignManager.Api.Models.AI;

public class AIMessage
{
    public long Id { get; set; }
    public Guid ConversationId { get; set; }
    public string Role { get; set; } = "user";
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public string? Model { get; set; }
    public string? ResponseId { get; set; }
    public long InputTokenCount { get; set; }
    public long OutputTokenCount { get; set; }
    public long TotalTokenCount { get; set; }
    public decimal EstimatedCostUsd { get; set; }
    public long DurationMs { get; set; }
    public string Status { get; set; } = "completed";
    public string SourcesJson { get; set; } = "[]";
    public int RetrievalInputTokens { get; set; }
    public string? RetrievalWarning { get; set; }

    public AIConversation Conversation { get; set; } = null!;
}

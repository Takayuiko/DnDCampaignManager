namespace DnDCampaignManager.Api.Models.AI;

public class AIConversation
{
    public Guid Id { get; set; }
    public int UserId { get; set; }
    public string Title { get; set; } = "New AI Conversation";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public DnDCampingManager.Api.Models.User User { get; set; } = null!;
    public ICollection<AIMessage> Messages { get; set; } = new List<AIMessage>();
}

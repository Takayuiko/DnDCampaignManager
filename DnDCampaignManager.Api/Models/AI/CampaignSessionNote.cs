using DnDCampingManager.Api.Models;

namespace DnDCampaignManager.Api.Models.AI;

public class CampaignSessionNote
{
    public long Id { get; set; }
    public int CampaignId { get; set; }
    public Campaign Campaign { get; set; } = null!;
    public int SessionNumber { get; set; }
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
    public DateOnly PlayedOn { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public string IndexStatus { get; set; } = "pending";
    public string? EmbeddingModel { get; set; }
    public int EmbeddingDimensions { get; set; }
    public int EmbeddingInputTokens { get; set; }
    public ICollection<CampaignKnowledgeChunk> Chunks { get; set; } = new List<CampaignKnowledgeChunk>();
}

public class CampaignKnowledgeChunk
{
    public long Id { get; set; }
    public long SessionNoteId { get; set; }
    public CampaignSessionNote SessionNote { get; set; } = null!;
    public int Position { get; set; }
    public string Content { get; set; } = "";
    public float[] Embedding { get; set; } = [];
}

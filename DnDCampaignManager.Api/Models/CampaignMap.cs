using DnDCampingManager.Api.Models;

namespace DnDCampaignManager.Api.Models;

public sealed class CampaignMap
{
    public long Id { get; set; }
    public int CampaignId { get; set; }
    public Campaign Campaign { get; set; } = null!;
    public string Title { get; set; } = "";
    public string NormalizedTitle { get; set; } = "";
    public string Description { get; set; } = "";
    public byte[] Image { get; set; } = [];
    public byte[]? Thumbnail { get; set; }
    public string ImageContentType { get; set; } = "";
    public string LocationsJson { get; set; } = "[]";
    public string IndexStatus { get; set; } = "pending";
    public Guid IndexRevision { get; set; } = Guid.NewGuid();
    public Guid? IndexLeaseId { get; set; }
    public DateTime? IndexLeaseUntil { get; set; }
    public string? EmbeddingModel { get; set; }
    public int EmbeddingDimensions { get; set; }
    public ICollection<MapKnowledgeChunk> Chunks { get; set; } = new List<MapKnowledgeChunk>();
}

public sealed class MapKnowledgeChunk
{
    public long Id { get; set; }
    public long MapId { get; set; }
    public CampaignMap Map { get; set; } = null!;
    public int Position { get; set; }
    public string Content { get; set; } = "";
    public float[] Embedding { get; set; } = [];
}

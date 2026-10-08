namespace DnDCampaignManager.Api.Models;

public sealed class CampaignNpc
{
    public long Id { get; set; }
    public int CampaignId { get; set; }
    public Campaign Campaign { get; set; } = null!;
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public bool IsPartyMember { get; set; }
    public long? MapId { get; set; }
    public CampaignMap? Map { get; set; }
    public string LocationName { get; set; } = "";
    public byte[]? Image { get; set; }
    public byte[]? OriginalImage { get; set; }
    public string? OriginalImageContentType { get; set; }
    public Guid Version { get; set; } = Guid.NewGuid();
}

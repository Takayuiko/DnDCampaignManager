namespace DnDCampaignManager.Api.Models;

public sealed class CampaignItem
{
    public int Id { get; set; }
    public int CampaignId { get; set; }
    public Campaign Campaign { get; set; } = null!;
    public string Name { get; set; } = "";
    public string NormalizedName { get; set; } = "";
    public string Category { get; set; } = "";
    public string Description { get; set; } = "";
    public decimal? WeightLb { get; set; }
    public decimal? CostGp { get; set; }
    public string? Source { get; set; }
}

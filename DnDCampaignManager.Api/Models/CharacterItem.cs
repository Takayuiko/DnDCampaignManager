namespace DnDCampaignManager.Api.Models;

// One inventory entry per assignment, allowing distinct notes for copies of an item.
public sealed class CharacterItem
{
    public int Id { get; set; }
    public int CampaignId { get; set; }
    public int CharacterId { get; set; }
    public Character Character { get; set; } = null!;
    public int ItemId { get; set; }
    public CampaignItem Item { get; set; } = null!;
    public int Quantity { get; set; } = 1;
    public string Notes { get; set; } = "";
    public DateTime AssignedAtUtc { get; set; } = DateTime.UtcNow;
}

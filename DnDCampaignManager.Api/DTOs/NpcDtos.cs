using System.ComponentModel.DataAnnotations;

namespace DnDCampaignManager.Api.DTOs;

public sealed class SaveNpcRequest
{
    [Required, StringLength(120)] public string Name { get; set; } = "";
    [Required, StringLength(4000)] public string Description { get; set; } = "";
    [Range(1, long.MaxValue)] public long MapId { get; set; }
    [Required, StringLength(120)] public string LocationName { get; set; } = "";
    public bool IsPartyMember { get; set; }
    public IFormFile? Image { get; set; }
    public bool RemoveImage { get; set; }
    public Guid? Version { get; set; }
}

public sealed record NpcDto(long Id, int CampaignId, string Name, string Description,
    bool IsPartyMember, long? MapId, string LocationName, bool HasImage, Guid Version);
public sealed record NpcCatalogDto(bool CanManage, IReadOnlyList<NpcDto> Npcs);

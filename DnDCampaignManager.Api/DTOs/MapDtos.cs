using System.ComponentModel.DataAnnotations;

namespace DnDCampaignManager.Api.DTOs;

public sealed record MapLocation([Required, StringLength(120)] string Name,
    [StringLength(4000)] string Description, [Range(0, 100)] double X, [Range(0, 100)] double Y);

public sealed class SaveMapRequest
{
    [Required, StringLength(160)] public string Title { get; set; } = "";
    [StringLength(4000)] public string Description { get; set; } = "";
    [Required, StringLength(240000)] public string LocationsJson { get; set; } = "[]";
    public IFormFile? Image { get; set; }
}

public sealed record MapDto(long Id, int CampaignId, string Title, string Description,
    IReadOnlyList<MapLocation> Locations, string IndexStatus);

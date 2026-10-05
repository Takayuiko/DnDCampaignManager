using System.ComponentModel.DataAnnotations;

namespace DnDCampaignManager.Api.DTOs.AI_DTO;

public sealed class SessionAudioUpload
{
    [Required]
    public IFormFile Audio { get; set; } = null!;
}

public sealed record SessionTranscriptDto(string Text, string Model);

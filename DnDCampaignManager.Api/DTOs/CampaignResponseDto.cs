namespace DnDCampaignManager.Api.DTOs
{
    public record CampaignResponseDto(
        int Id,
        string Name,
        string Description,
        int OwnerId,
        List<CharacterCampaignResponseDto> Characters,
        List<PlayerResponseDto> Players 
    );
}
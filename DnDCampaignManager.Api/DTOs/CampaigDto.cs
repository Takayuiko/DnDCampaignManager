namespace DnDCampaignManager.Api.DTOs
{
    public record CreateCampaignDto(
        string Name,
        string Description,
        int OwnerId
    );

    public class UpdateCampaignDto
    {
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
    }
}

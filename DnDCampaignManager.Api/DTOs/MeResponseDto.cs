namespace DnDCampaignManager.Api.DTOs
{
    public class MeResponseDto
    {
        public int Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = null!;
    }
}

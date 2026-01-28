using DnDCampingManager.Api.Models;

namespace DnDCampaignManager.Api.Models
{
    public class CharacterBackgroundOption
    {
        public int Id { get; set; }

        public int CampaignId { get; set; }
        public Campaign Campaign { get; set; } = null!;

        public int UserId { get; set; }
        public User User { get; set; } = null!;

        public string Name { get; set; } = null!;
        public string NormalizedName { get; set; } = null!;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}

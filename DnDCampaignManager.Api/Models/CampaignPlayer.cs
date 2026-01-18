using DnDCampingManager.Api.Models;

namespace DnDCampaignManager.Api.Models
{
    public class CampaignPlayer
    {
        public int CampaignId { get; set; }
        public Campaign Campaign { get; set; } = null!;

        public int UserId { get; set; }
        public User User { get; set; } = null!;

        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
    }
}

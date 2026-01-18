using DnDCampingManager.Api.Models;

namespace DnDCampaignManager.Api.Models
{
    public class Campaign
    {
        public int Id { get; set; }

        public string Name { get; set; } = null!;
        public string Description { get; set; } = null!;

        // Ownership
        public int OwnerId { get; set; }
        public User Owner { get; set; } = null!;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public List<CampaignPlayer> Players { get; set; } = new();
        public ICollection<Character> Characters { get; set; } = new List<Character>();
    }

}

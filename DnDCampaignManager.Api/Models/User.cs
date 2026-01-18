using DnDCampaignManager.Api.Models;

namespace DnDCampingManager.Api.Models
{
    public class User
    {
        public int Id { get; set; }
        public string Email { get; set; } = null!;
        public string PasswordHash { get; set; } = null!;
        public string Role { get; set; } = Roles.Player;
        public int TokenVersion { get; set; } = 0;
        public List<CampaignPlayer> Campaigns { get; set; } = new();
        public ICollection<Character> Characters { get; set; } = new List<Character>();
        public ICollection<Campaign> OwnedCampaigns { get; set; } = new List<Campaign>();
        public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
    }
}

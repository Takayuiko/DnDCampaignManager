using DnDCampaignManager.Api.Models;

namespace DnDCampaignManager.Api.DTOs
{
    public class CharacterSkillDto
    {
        public SkillType Skill { get; set; }
        public AbilityType Ability { get; set; }

        public bool IsProficient { get; set; }
        public bool IsExpertise { get; set; }

        public int MiscBonus { get; set; }
    }
}

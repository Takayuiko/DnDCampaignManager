namespace DnDCampaignManager.Api.Models
{
    public class CharacterSkill
    {
        public int Id { get; set; }

        public int CharacterId { get; set; }
        public Character Character { get; set; } = null!;

        public SkillType Skill { get; set; }
        public AbilityType Ability { get; set; }

        public bool IsProficient { get; set; }
        public bool IsExpertise { get; set; }

        public int MiscBonus { get; set; }
    }
}

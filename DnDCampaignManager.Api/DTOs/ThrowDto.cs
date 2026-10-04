namespace DnDCampaignManager.Api.DTOs
{
    public class SavingThrowDto
    {
        public bool IsProficient { get; set; }
        public int MiscBonus { get; set; }
    }

    public class SavingThrowsDto
    {
        public SavingThrowDto Strength { get; set; } = new();
        public SavingThrowDto Dexterity { get; set; } = new();
        public SavingThrowDto Constitution { get; set; } = new();
        public SavingThrowDto Intelligence { get; set; } = new();
        public SavingThrowDto Wisdom { get; set; } = new();
        public SavingThrowDto Charisma { get; set; } = new();
    }
}

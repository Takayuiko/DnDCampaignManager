namespace DnDCampaignManager.Api.Models
{
    public class CharacterAttack
    {
        public int Id { get; set; }

        public int CharacterId { get; set; }
        public Character Character { get; set; } = null!;

        public string Name { get; set; } = "";
        public int AttackBonus { get; set; } = 0;
        public string Damage { get; set; } = "";
    }
}

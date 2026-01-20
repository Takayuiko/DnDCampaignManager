namespace DnDCampaignManager.Api.DTOs
{
    public class CharacterAttackDto
    {
        public int? Id { get; set; }
        public string Name { get; set; } = "";
        public int AttackBonus { get; set; }
        public string Damage { get; set; } = "";
    }
}

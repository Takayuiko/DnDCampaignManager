using DnDCampingManager.Api.Models;

namespace DnDCampaignManager.Api.Models
{
    public class Character
    {
        public int Id { get; set; }

        [System.ComponentModel.DataAnnotations.ConcurrencyCheck]
        public Guid Version { get; set; } = Guid.NewGuid();

        // Ownership
        public int UserId { get; set; }
        public User User { get; set; } = null!;
        public int CampaignId { get; set; }
        public Campaign Campaign { get; set; } = null!;

        // Identity
        public string Name { get; set; } = null!;
        public string Class { get; set; } = null!;
        public string Race { get; set; } = null!;      
        public int Level { get; set; } = 1;
        public string Background { get; set; } = null!;
        public string Alignment { get; set; } = null!;
        public int ExperiencePoints { get; set; }

        // Core Stats
        public int Strength { get; set; }
        public int Dexterity { get; set; }
        public int Constitution { get; set; }
        public int Intelligence { get; set; }
        public int Wisdom { get; set; }
        public int Charisma { get; set; }

        // Combat Stats
        public int ProficiencyBonus { get; set; }
        public int ArmorClass { get; set; }
        public int Initiative { get; set; }
        public int Speed { get; set; }

        // Hit Points
        public int HitPointMax { get; set; }
        public int HitPointCurrent { get; set; }
        public int HitPointTemporary { get; set; }

        public bool Inspiration { get; set; }

        // Hit dice
        public string? HitDiceDie { get; set; }
        public int? HitDiceTotal { get; set; }
        public int? HitDiceRemaining { get; set; }

        // Saving throws
        public bool SaveStrProficient { get; set; }
        public int SaveStrMiscBonus { get; set; }
        public bool SaveDexProficient { get; set; }
        public int SaveDexMiscBonus { get; set; }
        public bool SaveConProficient { get; set; }
        public int SaveConMiscBonus { get; set; }
        public bool SaveIntProficient { get; set; }
        public int SaveIntMiscBonus { get; set; }
        public bool SaveWisProficient { get; set; }
        public int SaveWisMiscBonus { get; set; }
        public bool SaveChaProficient { get; set; }
        public int SaveChaMiscBonus { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Attacks and Skills
        public List<CharacterAttack> Attacks { get; set; } = new();
        public List<CharacterSkill> Skills { get; set; } = new();
    }

}

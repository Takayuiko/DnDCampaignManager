using DnDCampaignManager.Api.Models;

namespace DnDCampaignManager.Api.DTOs
{
    public class CharacterResponseDto
    {
        public int Id { get; set; }
        public int UserId { get; set; }

        public string Name { get; set; } = null!;
        public string Class { get; set; } = null!;
        public string Race { get; set; } = null!;
        public int Level { get; set; }
        public string Background { get; set; } = null!;
        public string Alignment { get; set; } = null!;
        public int ExperiencePoints { get; set; }

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

        // Hit Dice
        public string HitDiceDie { get; set; } = "d8";
        public int HitDiceTotal { get; set; } = 1;
        public int HitDiceRemaining { get; set; } = 1;

        // Death Saves
        public int DeathSaveSuccesses { get; set; }
        public int DeathSaveFailures { get; set; }

        // Attacks and Skills
        public List<CharacterAttackDto> Attacks { get; set; } = new();
        public List<CharacterSkillDto> Skills { get; set; } = new();
    }

    public class CharacterCampaignResponseDto
    {
        public int Id { get; set; }
        public int UserId { get; set; }

        public string Name { get; set; } = null!;
        public string Class { get; set; } = null!;
        public string Race { get; set; } = null!;
        public int Level { get; set; }
    }
}
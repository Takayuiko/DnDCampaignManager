using DnDCampaignManager.Api.Models;

namespace DnDCampaignManager.Api.Services
{
    public static class SkillDefaults
    {
        public static List<CharacterSkill> CompleteSkills(IEnumerable<CharacterSkill> skills, int characterId)
        {
            var result = skills.ToList();
            var existing = result.Select(s => s.Skill).ToHashSet();
            result.AddRange(CreateDefaultSkills(characterId).Where(s => !existing.Contains(s.Skill)));
            return result;
        }

        public static List<CharacterSkill> CreateDefaultSkills(int characterId)
        {
            return new List<CharacterSkill>
            {
                New(characterId, SkillType.Acrobatics, AbilityType.Dexterity),
                New(characterId, SkillType.AnimalHandling, AbilityType.Wisdom),
                New(characterId, SkillType.Arcana, AbilityType.Intelligence),
                New(characterId, SkillType.Athletics, AbilityType.Strength),
                New(characterId, SkillType.Deception, AbilityType.Charisma),
                New(characterId, SkillType.History, AbilityType.Intelligence),
                New(characterId, SkillType.Insight, AbilityType.Wisdom),
                New(characterId, SkillType.Intimidation, AbilityType.Charisma),
                New(characterId, SkillType.Investigation, AbilityType.Intelligence),
                New(characterId, SkillType.Medicine, AbilityType.Wisdom),
                New(characterId, SkillType.Nature, AbilityType.Intelligence),
                New(characterId, SkillType.Perception, AbilityType.Wisdom),
                New(characterId, SkillType.Performance, AbilityType.Charisma),
                New(characterId, SkillType.Persuasion, AbilityType.Charisma),
                New(characterId, SkillType.Religion, AbilityType.Intelligence),
                New(characterId, SkillType.SleightOfHand, AbilityType.Dexterity),
                New(characterId, SkillType.Stealth, AbilityType.Dexterity),
                New(characterId, SkillType.Survival, AbilityType.Wisdom),
            };
        }

        private static CharacterSkill New(int characterId, SkillType skill, AbilityType ability)
            => new CharacterSkill
            {
                CharacterId = characterId,
                Skill = skill,
                Ability = ability,
                IsProficient = false,
                IsExpertise = false
            };
    }
}

using DnDCampaignManager.Api.DTOs;
using DnDCampaignManager.Api.Models;
using System.Linq.Expressions;

namespace DnDCampaignManager.Api.Services;

public static class CharacterMapping
{
    public static readonly Expression<Func<Character, CharacterListItemDto>> ListItem = c => new CharacterListItemDto(
        c.Id, c.Name, c.Class, c.Race, c.Level, c.Background,
        c.Alignment, c.ExperiencePoints, c.UserId,
        c.Strength, c.Dexterity, c.Constitution, c.Intelligence,
        c.Wisdom, c.Charisma, c.ProficiencyBonus, c.ArmorClass,
        c.Initiative, c.Speed, c.HitPointMax, c.HitPointCurrent,
        c.HitPointTemporary);

    public static CharacterResponseDto ToResponse(Character character)
    {
        return new CharacterResponseDto
        {
            Id = character.Id,
            UserId = character.UserId,
            Name = character.Name,
            Class = character.Class,
            Race = character.Race,
            Level = character.Level,
            Background = character.Background,
            Alignment = character.Alignment,
            ExperiencePoints = character.ExperiencePoints,
            Strength = character.Strength,
            Dexterity = character.Dexterity,
            Constitution = character.Constitution,
            Intelligence = character.Intelligence,
            Wisdom = character.Wisdom,
            Charisma = character.Charisma,
            ProficiencyBonus = character.ProficiencyBonus,
            ArmorClass = character.ArmorClass,
            Initiative = character.Initiative,
            Speed = character.Speed,
            HitPointMax = character.HitPointMax,
            HitPointCurrent = character.HitPointCurrent,
            HitPointTemporary = character.HitPointTemporary,
            Inspiration = character.Inspiration,
            HitDice = new HitDiceDto(character.HitDiceDie ?? string.Empty, character.HitDiceTotal ?? 0, character.HitDiceRemaining ?? 0),
            SavingThrows = new SavingThrowsDto
            {
                Strength = new SavingThrowDto { IsProficient = character.SaveStrProficient, MiscBonus = character.SaveStrMiscBonus },
                Dexterity = new SavingThrowDto { IsProficient = character.SaveDexProficient, MiscBonus = character.SaveDexMiscBonus },
                Constitution = new SavingThrowDto { IsProficient = character.SaveConProficient, MiscBonus = character.SaveConMiscBonus },
                Intelligence = new SavingThrowDto { IsProficient = character.SaveIntProficient, MiscBonus = character.SaveIntMiscBonus },
                Wisdom = new SavingThrowDto { IsProficient = character.SaveWisProficient, MiscBonus = character.SaveWisMiscBonus },
                Charisma = new SavingThrowDto { IsProficient = character.SaveChaProficient, MiscBonus = character.SaveChaMiscBonus },
            },
            Attacks = character.Attacks.Select(a => new CharacterAttackDto
            {
                Id = a.Id,
                Name = a.Name,
                AttackBonus = a.AttackBonus,
                Damage = a.Damage
            }).ToList(),
            Skills = SkillDefaults.CompleteSkills(character.Skills, character.Id).OrderBy(s => s.Skill)
            .Select(s => new CharacterSkillDto
            {
                Skill = s.Skill,
                Ability = s.Ability,
                IsProficient = s.IsProficient,
                IsExpertise = s.IsExpertise,
                MiscBonus = s.MiscBonus
            }).ToList()
        };
    }

    public static void Apply(Character character, ICharacterWriteDto request, bool creating = false)
    {
        character.Name = request.Name;
        character.Class = request.Class;
        character.Race = request.Race;
        character.Level = request.Level;
        character.Background = request.Background;
        character.Alignment = request.Alignment;
        character.ExperiencePoints = request.ExperiencePoints;
        character.Strength = request.Strength;
        character.Dexterity = request.Dexterity;
        character.Constitution = request.Constitution;
        character.Intelligence = request.Intelligence;
        character.Wisdom = request.Wisdom;
        character.Charisma = request.Charisma;
        character.ProficiencyBonus = request.ProficiencyBonus;
        character.ArmorClass = request.ArmorClass;
        character.Initiative = request.Initiative;
        character.Speed = request.Speed;
        character.HitPointMax = request.HitPointMax;
        character.HitPointCurrent = request.HitPointCurrent;
        character.HitPointTemporary = request.HitPointTemporary;
        character.Inspiration = request.Inspiration;

        if (request.HitDice is not null)
        {
            character.HitDiceDie = request.HitDice.Die;
            character.HitDiceTotal = request.HitDice.Total;
            character.HitDiceRemaining = request.HitDice.Remaining;
        }

        var st = request.SavingThrows ?? new SavingThrowsDto();

        character.SaveStrProficient = st.Strength.IsProficient;
        character.SaveStrMiscBonus = st.Strength.MiscBonus;

        character.SaveDexProficient = st.Dexterity.IsProficient;
        character.SaveDexMiscBonus = st.Dexterity.MiscBonus;

        character.SaveConProficient = st.Constitution.IsProficient;
        character.SaveConMiscBonus = st.Constitution.MiscBonus;

        character.SaveIntProficient = st.Intelligence.IsProficient;
        character.SaveIntMiscBonus = st.Intelligence.MiscBonus;

        character.SaveWisProficient = st.Wisdom.IsProficient;
        character.SaveWisMiscBonus = st.Wisdom.MiscBonus;

        character.SaveChaProficient = st.Charisma.IsProficient;
        character.SaveChaMiscBonus = st.Charisma.MiscBonus;

        if (request.Attacks is not null)
        {
            character.Attacks.Clear();
            foreach (var attack in request.Attacks.Where(attack => !creating || !string.IsNullOrWhiteSpace(attack.Name)))
            {
                character.Attacks.Add(new CharacterAttack { Name = attack.Name, AttackBonus = attack.AttackBonus, Damage = attack.Damage });
            }
        }
        ApplySkills(character, request.Skills);
    }

    private static void ApplySkills(Character character, List<CharacterSkillDto>? incomingSkills)
    {
        character.Skills = SkillDefaults.CompleteSkills(character.Skills, character.Id);
        foreach (var incoming in incomingSkills ?? [])
        {
            var skill = character.Skills.Single(s => s.Skill == incoming.Skill);
            skill.IsProficient = incoming.IsProficient;
            skill.IsExpertise = incoming.IsExpertise;
            skill.MiscBonus = incoming.MiscBonus;
        }
    }
}

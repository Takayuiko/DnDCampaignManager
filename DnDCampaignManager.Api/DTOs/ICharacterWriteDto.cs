using System.ComponentModel.DataAnnotations;
using DnDCampaignManager.Api.Services;

namespace DnDCampaignManager.Api.DTOs;

public interface ICharacterWriteDto : IValidatableObject
{
    string Name { get; }
    string Class { get; }
    string Race { get; }
    int Level { get; }
    string Background { get; }
    string Alignment { get; }
    int ExperiencePoints { get; }
    int UserId { get; }
    int Strength { get; }
    int Dexterity { get; }
    int Constitution { get; }
    int Intelligence { get; }
    int Wisdom { get; }
    int Charisma { get; }
    int ProficiencyBonus { get; }
    int ArmorClass { get; }
    int Initiative { get; }
    int Speed { get; }
    int HitPointMax { get; }
    int HitPointCurrent { get; }
    int HitPointTemporary { get; }
    bool Inspiration { get; }
    HitDiceDto? HitDice { get; }
    SavingThrowsDto SavingThrows { get; }
    List<CharacterAttackDto>? Attacks { get; }
    List<CharacterSkillDto>? Skills { get; }

    IEnumerable<ValidationResult> IValidatableObject.Validate(ValidationContext context)
        => CharacterValidation.Errors(this);
}

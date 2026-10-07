using System.ComponentModel.DataAnnotations;
using DnDCampaignManager.Api.DTOs;

namespace DnDCampaignManager.Api.Services;

public static class CharacterValidation
{
    public static IEnumerable<ValidationResult> Errors(ICharacterWriteDto request)
    {
        if (request.Skills is { } skills && (skills.Any(skill => skill is null || !Enum.IsDefined(skill.Skill) || !Enum.IsDefined(skill.Ability)) ||
            skills.Select(skill => skill.Skill).Distinct().Count() != skills.Count))
            yield return new ValidationResult("Skills must contain unique, valid skill and ability values.", [nameof(request.Skills)]);
        if (request.Attacks?.Any(attack => attack is null) == true)
            yield return new ValidationResult("Attacks cannot contain null items.", [nameof(request.Attacks)]);
        if (request.SavingThrows is { } saves && (saves.Strength is null || saves.Dexterity is null || saves.Constitution is null ||
            saves.Intelligence is null || saves.Wisdom is null || saves.Charisma is null))
            yield return new ValidationResult("Each saving throw must contain proficiency and bonus values.", [nameof(request.SavingThrows)]);
    }
}

using System.ComponentModel.DataAnnotations;
using DnDCampaignManager.Api.DTOs;

namespace DnDCampaignManager.Api.Services;

public static class CharacterValidation
{
    public static IEnumerable<ValidationResult> Errors(ICharacterWriteDto request)
    {
        foreach (var (name, value, maximum, required) in new[] {
            (nameof(request.Name), request.Name, 120, true),
            (nameof(request.Class), request.Class, 120, true),
            (nameof(request.Race), request.Race, 120, true),
            (nameof(request.Background), request.Background, 120, false),
            (nameof(request.Alignment), request.Alignment, 80, false)
        })
            if (!Text(value, maximum, required))
                yield return Error(name, $"{name} must {(required ? "contain nonblank text and " : "")}be at most {maximum} characters.");

        foreach (var (name, value, minimum, maximum) in new[] {
            (nameof(request.Level), request.Level, 1, 100),
            (nameof(request.ExperiencePoints), request.ExperiencePoints, 0, 1000000000),
            (nameof(request.Strength), request.Strength, 0, 100),
            (nameof(request.Dexterity), request.Dexterity, 0, 100),
            (nameof(request.Constitution), request.Constitution, 0, 100),
            (nameof(request.Intelligence), request.Intelligence, 0, 100),
            (nameof(request.Wisdom), request.Wisdom, 0, 100),
            (nameof(request.Charisma), request.Charisma, 0, 100),
            (nameof(request.ProficiencyBonus), request.ProficiencyBonus, 0, 100),
            (nameof(request.ArmorClass), request.ArmorClass, 0, 1000),
            (nameof(request.Initiative), request.Initiative, -1000, 1000),
            (nameof(request.Speed), request.Speed, 0, 10000),
            (nameof(request.HitPointMax), request.HitPointMax, 0, 1000000),
            (nameof(request.HitPointCurrent), request.HitPointCurrent, 0, 1000000),
            (nameof(request.HitPointTemporary), request.HitPointTemporary, 0, 1000000)
        })
            if (value < minimum || value > maximum)
                yield return Error(name, $"{name} must be between {minimum} and {maximum}.");
        if (request.HitPointCurrent > request.HitPointMax)
            yield return Error(nameof(request.HitPointCurrent), "Current HP cannot exceed maximum HP. Use temporary HP for additional hit points.");

        if (request.HitDice is { } dice && (!Text(dice.Die, 16, dice.Total > 0) ||
            dice.Total is < 0 or > 1000 || dice.Remaining < 0 || dice.Remaining > dice.Total))
            yield return Error(nameof(request.HitDice), "Hit dice require a die label up to 16 characters when total is positive, total between 0 and 1000, and remaining between 0 and total.");

        if (request.Skills is { } skills)
        {
            if (skills.Count > 18 || skills.Any(skill => skill is null || !Enum.IsDefined(skill.Skill) || !Enum.IsDefined(skill.Ability)) ||
                skills.Select(skill => skill.Skill).Distinct().Count() != skills.Count)
                yield return Error(nameof(request.Skills), "Skills must contain at most 18 unique, valid skill and ability values.");
            if (skills.Any(skill => skill is not null && skill.MiscBonus is < -1000 or > 1000))
                yield return Error(nameof(request.Skills), "Skill bonuses must be between -1000 and 1000.");
        }
        if (request.Attacks is { } attacks && (attacks.Count > 50 || attacks.Any(attack => attack is null ||
            !Text(attack.Name, 120, true) || !Text(attack.Damage, 500, false) || attack.AttackBonus is < -1000 or > 1000)))
            yield return Error(nameof(request.Attacks), "Attacks must contain at most 50 items, each with a nonblank name up to 120 characters, damage text up to 500 characters, and a bonus between -1000 and 1000.");

        if (request.SavingThrows is not { } saves || saves.Strength is null || saves.Dexterity is null || saves.Constitution is null ||
            saves.Intelligence is null || saves.Wisdom is null || saves.Charisma is null)
            yield return Error(nameof(request.SavingThrows), "Each saving throw must contain proficiency and bonus values.");
        else if (new[] { saves.Strength, saves.Dexterity, saves.Constitution, saves.Intelligence, saves.Wisdom, saves.Charisma }
            .Any(save => save.MiscBonus is < -1000 or > 1000))
            yield return Error(nameof(request.SavingThrows), "Saving throw bonuses must be between -1000 and 1000.");
    }

    private static bool Text(string? value, int maximum, bool required) =>
        value is not null && value.Length <= maximum && (!required || !string.IsNullOrWhiteSpace(value));
    private static ValidationResult Error(string member, string message) => new(message, [member]);
}

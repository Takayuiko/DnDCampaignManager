namespace DnDCampaignManager.Api.DTOs
{
    public record UpdateCharacterDto(
        string Name,
        string Class,
        string Race,
        int Level,
        string Background,
        string Alignment,
        int ExperiencePoints,
        int UserId,
        int Strength,
        int Dexterity,
        int Constitution,
        int Intelligence,
        int Wisdom,
        int Charisma,
        int ProficiencyBonus,
        int ArmorClass,
        int Initiative,
        int Speed,
        int HitPointMax,
        int HitPointCurrent,
        int HitPointTemporary,
        bool Inspiration,
        string HitDiceDie,
        int HitDiceTotal,
        int HitDiceRemaining,
        int DeathSaveSuccesses,
        int DeathSaveFailures,
        List<CharacterAttackDto>? Attacks,
        List<CharacterSkillDto> Skills
    );
}

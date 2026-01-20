namespace DnDCampaignManager.Api.DTOs;

public record AttackDto(
    int? Id,
    string Name,
    int AttackBonus,
    string Damage
);

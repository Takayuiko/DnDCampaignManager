namespace DnDCampaignManager.Api.DTOs;

public record HitDiceDto(
    string Die,     
    int Total,
    int Remaining
);

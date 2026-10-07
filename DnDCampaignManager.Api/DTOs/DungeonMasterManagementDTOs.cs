using System.ComponentModel.DataAnnotations;

namespace DnDCampaignManager.Api.DTOs;

public sealed record PromoteDungeonMasterDto([Required, EmailAddress] string Email);
public sealed record DungeonMasterDto(int Id, string Email, string Role, bool IsAdmin, int CampaignCount);
public sealed record DmRemovalPreviewDto(int UserId, string Email, int CampaignCount, int CharacterCount,
    int SessionNoteCount, int CampaignConversationCount, int GeneralConversationCount, int PreservedCharacterCount);
public sealed record RemoveDungeonMasterDto([Range(0, int.MaxValue)] int ExpectedCampaignCount);

using DnDCampaignManager.Api.Models;
using DnDCampingManager.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace DnDCampaignManager.Api.Services;

public static class CampaignAuthorization
{
    public sealed record Access(int OwnerId, bool IsPlayer)
    {
        public bool CanAccess(int userId) => OwnerId == userId || IsPlayer;
    }

    public static Task<Access?> GetAccessAsync(DnDxDbContext db, int campaignId, int userId) =>
        db.Campaigns.Where(c => c.Id == campaignId)
            .Select(c => new Access(c.OwnerId, c.Players.Any(p => p.UserId == userId)))
            .SingleOrDefaultAsync();

    public static IQueryable<Campaign> AccessibleTo(this IQueryable<Campaign> campaigns, int userId) =>
        campaigns.Where(c => c.OwnerId == userId || c.Players.Any(p => p.UserId == userId));

    // Use only when Players has been loaded (including inside an existing row-lock transaction).
    public static bool CanAccess(Campaign campaign, int userId) =>
        campaign.OwnerId == userId || campaign.Players.Any(p => p.UserId == userId);

    public static Task<bool> CanAccessAsync(DnDxDbContext db, int campaignId, int userId, CancellationToken ct = default) =>
        db.Campaigns.AccessibleTo(userId).AnyAsync(c => c.Id == campaignId, ct);

    public static Task<bool> CanManageAsync(DnDxDbContext db, int campaignId, int userId, CancellationToken ct = default) =>
        db.Campaigns.AnyAsync(c => c.Id == campaignId && c.OwnerId == userId, ct);

    public static async Task<bool> CanUseCharacterAsync(DnDxDbContext db, Character character, int userId, bool isDm) =>
        (isDm && character.Campaign.OwnerId == userId) ||
        (character.UserId == userId && await db.CampaignPlayer.AnyAsync(p =>
            p.CampaignId == character.CampaignId && p.UserId == userId));
}

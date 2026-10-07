using DnDCampaignManager.Api.DTOs;
using DnDCampaignManager.Api.Models;
using DnDCampingManager.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace DnDCampaignManager.Api.Services;

public sealed class DmManagementException(int status, string message) : Exception(message)
{
    public int Status { get; } = status;
}

public sealed class DungeonMasterManagementService(DnDxDbContext db)
{
    private async Task RequireAdmin(int actorId, CancellationToken ct)
    {
        if (!await db.Users.AnyAsync(u => u.Id == actorId && u.IsAdmin && u.Role == Roles.DM, ct))
            throw new DmManagementException(403, "Administrator access is required.");
    }

    public async Task<List<DungeonMasterDto>> ListAsync(int actorId, CancellationToken ct)
    {
        await RequireAdmin(actorId, ct);
        return await db.Users.AsNoTracking().Where(u => u.Role == Roles.DM).OrderBy(u => u.Email)
            .Select(u => new DungeonMasterDto(u.Id, u.Email, u.Role, u.IsAdmin, u.OwnedCampaigns.Count)).ToListAsync(ct);
    }

    public async Task<DungeonMasterDto> PromoteAsync(int actorId, string email, CancellationToken ct)
    {
        await RequireAdmin(actorId, ct);
        var normalized = email.Trim().ToLowerInvariant();
        var ids = await db.Users.Where(u => u.NormalizedEmail == normalized).Select(u => u.Id).Take(2).ToListAsync(ct);
        if (ids.Count == 0) throw new DmManagementException(404, "No registered user has that email.");
        if (ids.Count != 1) throw new DmManagementException(409, "Multiple accounts match this email.");
        await using var transaction = db.Database.CurrentTransaction is null ? await db.Database.BeginTransactionAsync(ct) : null;
        var user = await db.Users.FromSqlInterpolated($"SELECT * FROM \"Users\" WHERE \"Id\" = {ids[0]} FOR UPDATE").SingleAsync(ct);
        if (user.Role != Roles.DM)
        {
            user.Role = Roles.DM;
            user.TokenVersion++;
            await db.RefreshTokens.Where(t => t.UserId == user.Id).ExecuteDeleteAsync(ct);
            await db.SaveChangesAsync(ct);
        }
        if (transaction is not null) await transaction.CommitAsync(ct);
        return new DungeonMasterDto(user.Id, user.Email, user.Role, user.IsAdmin,
            await db.Campaigns.CountAsync(c => c.OwnerId == user.Id, ct));
    }

    public async Task<DmRemovalPreviewDto> PreviewAsync(int actorId, int userId, CancellationToken ct)
    {
        await RequireAdmin(actorId, ct);
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == userId && u.Role == Roles.DM, ct)
            ?? throw new DmManagementException(404, "Dungeon Master not found.");
        if (user.IsAdmin) throw new DmManagementException(409, "The seeded administrator cannot be demoted here.");
        var campaigns = db.Campaigns.Where(c => c.OwnerId == userId).Select(c => c.Id);
        return new DmRemovalPreviewDto(userId, user.Email, await campaigns.CountAsync(ct),
            await db.Characters.CountAsync(c => campaigns.Contains(c.CampaignId), ct),
            await db.CampaignSessionNotes.CountAsync(n => campaigns.Contains(n.CampaignId), ct),
            await db.AIConversations.CountAsync(c => c.CampaignId != null && campaigns.Contains(c.CampaignId.Value), ct),
            await db.AIConversations.CountAsync(c => c.UserId == userId && c.CampaignId == null, ct),
            await db.Characters.CountAsync(c => c.UserId == userId && !campaigns.Contains(c.CampaignId), ct));
    }

    public async Task RemoveAsync(int actorId, int userId, int expectedCampaignCount, CancellationToken ct)
    {
        await RequireAdmin(actorId, ct);
        await using var transaction = db.Database.CurrentTransaction is null ? await db.Database.BeginTransactionAsync(ct) : null;
        var user = await db.Users.FromSqlInterpolated($"SELECT * FROM \"Users\" WHERE \"Id\" = {userId} FOR UPDATE").SingleOrDefaultAsync(ct)
            ?? throw new DmManagementException(404, "Dungeon Master not found.");
        if (user.IsAdmin || userId == actorId) throw new DmManagementException(409, "The seeded administrator cannot be demoted here.");
        if (user.Role != Roles.DM) throw new DmManagementException(409, "This user is no longer a Dungeon Master.");
        if (await db.Campaigns.CountAsync(c => c.OwnerId == userId, ct) != expectedCampaignCount)
            throw new DmManagementException(409, "Campaigns changed. Review the removal preview again.");
        // Campaign FK cascades remove every owned campaign's memberships, characters, session knowledge and all users' chats.
        await db.Campaigns.Where(c => c.OwnerId == userId).ExecuteDeleteAsync(ct);
        await db.AIConversations.Where(c => c.UserId == userId && c.CampaignId == null).ExecuteDeleteAsync(ct);
        await db.RefreshTokens.Where(t => t.UserId == userId).ExecuteDeleteAsync(ct);
        user.Role = Roles.Player;
        user.TokenVersion++;
        await db.SaveChangesAsync(ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
    }
}

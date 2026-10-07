using DnDCampingManager.Api.Data;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using DnDCampaignManager.Api.Models;

namespace DnDCampaignManager.Api.Services;

public sealed class CurrentTokenValidator(DnDxDbContext db)
{
    public async Task<bool> IsCurrentAsync(ClaimsPrincipal? principal, CancellationToken ct)
    {
        if (!int.TryParse(principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ||
            !int.TryParse(principal?.FindFirstValue("token_version"), out var version)) return false;
        var user = await db.Users.AsNoTracking().Where(x => x.Id == id)
            .Select(x => new { x.Role, x.IsAdmin, x.TokenVersion }).SingleOrDefaultAsync(ct);
        return user is not null && user.TokenVersion == version && principal!.IsInRole(user.Role) &&
            principal.IsInRole(Roles.Admin) == (user.IsAdmin && user.Role == Roles.DM) &&
            principal.IsInRole(Roles.DM) == (user.Role == Roles.DM);
    }
}

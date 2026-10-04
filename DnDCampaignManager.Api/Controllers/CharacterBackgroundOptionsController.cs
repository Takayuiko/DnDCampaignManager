using DnDCampingManager.Api.Data;
using DnDCampaignManager.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace DnDCampaignManager.Api.Controllers
{
    [ApiController]
    [Route("api/campaigns/{campaignId}/character-backgrounds")]
    public class CharacterBackgroundOptionsController : ControllerBase
    {
        private readonly DnDxDbContext _dnDxDbContext;

        private static readonly string[] DefaultBackgrounds =
        {
            "Acolyte","Charlatan","Criminal","Entertainer","Folk Hero","Guild Artisan",
            "Hermit","Noble","Outlander","Sage","Sailor","Soldier","Urchin"
        };

        public CharacterBackgroundOptionsController(DnDxDbContext db) => _dnDxDbContext = db;

        private int GetUserId()
        {
            var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(raw, out var id) ? id : 0;
        }

        public record CharacterBackgroundOptionDto(int Id, string Name, bool IsCustom);
        public record AddBackgroundRequest(string Name);

        [HttpGet]
        public async Task<ActionResult<IEnumerable<CharacterBackgroundOptionDto>>> Get(int campaignId)
        {
            var userId = GetUserId();
            if (userId <= 0) return Unauthorized();

            var hasAccess = await _dnDxDbContext.Campaigns.AnyAsync(c =>
                c.Id == campaignId &&
                (c.OwnerId == userId || c.Players.Any(p => p.UserId == userId)));

            if (!hasAccess) return Forbid();

            var custom = await _dnDxDbContext.CharacterBackgroundOptions
                .Where(x => x.CampaignId == campaignId)
                .OrderBy(x => x.Name)
                .Select(x => new CharacterBackgroundOptionDto(x.Id, x.Name, true))
                .ToListAsync();

            var defaults = DefaultBackgrounds
                .OrderBy(x => x)
                .Select(x => new CharacterBackgroundOptionDto(0, x, false));

            return Ok(defaults.Concat(custom));
        }

        [HttpPost]
        public async Task<ActionResult<CharacterBackgroundOptionDto>> Add(int campaignId, [FromBody] AddBackgroundRequest req)
        {
            var userId = GetUserId();
            if (userId <= 0) return Unauthorized();

            var hasAccess = await _dnDxDbContext.Campaigns.AnyAsync(c =>
                c.Id == campaignId &&
                (c.OwnerId == userId || c.Players.Any(p => p.UserId == userId)));

            if (!hasAccess) return Forbid();

            var name = (req?.Name ?? "").Trim();
            if (name.Length < 2 || name.Length > 50)
                return BadRequest("Background name must be 2–50 characters.");

            if (DefaultBackgrounds.Any(d => d.Equals(name, StringComparison.OrdinalIgnoreCase)))
                return Conflict("That background already exists in the default list.");

            var normalized = name.ToUpperInvariant();
            var exists = await _dnDxDbContext.CharacterBackgroundOptions.AnyAsync(x =>
                x.CampaignId == campaignId && x.NormalizedName == normalized);

            if (exists) return Conflict("You already added that background.");

            var option = new CharacterBackgroundOption
            {
                CampaignId = campaignId,
                UserId = userId,
                Name = name,
                NormalizedName = normalized
            };

            _dnDxDbContext.CharacterBackgroundOptions.Add(option);
            await _dnDxDbContext.SaveChangesAsync();

            return Ok(new CharacterBackgroundOptionDto(option.Id, option.Name, true));
        }
    }
}

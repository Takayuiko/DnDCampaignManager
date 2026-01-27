using DnDCampingManager.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace DnDCampaignManager.Api.Controllers
{
    [ApiController]
    [Route("api/campaigns/{campaignId}/character-classes")]
    public class CharacterClassOptionsController : ControllerBase
    {
        private readonly DnDxDbContext _db;

        // Server-side defaults so the UI never drifts
        private static readonly string[] DefaultClasses =
        {
            "Barbarian","Bard","Cleric","Druid","Fighter","Monk","Paladin",
            "Ranger","Rogue","Sorcerer","Warlock","Wizard","Artificer"
        };

        public CharacterClassOptionsController(DnDxDbContext db) => _db = db;

        private int GetUserId()
        {
            var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(raw, out var id) ? id : 0;
        }

        public record ClassOptionDto(int Id, string Name, bool IsCustom);
        public record CharacterClassOption(string Name);

        [HttpGet]
        public async Task<ActionResult<IEnumerable<ClassOptionDto>>> Get(int campaignId)
        {
            var userId = GetUserId();
            if (userId <= 0) return Unauthorized();

            var hasAccess = await _db.Campaigns.AnyAsync(c =>
                c.Id == campaignId &&
                (c.OwnerId == userId || c.Players.Any(p => p.UserId == userId)));

            if (!hasAccess) return Forbid();

            var custom = await _db.CharacterClassOptions
                .Where(x => x.CampaignId == campaignId)
                .OrderBy(x => x.Name)
                .Select(x => new ClassOptionDto(x.Id, x.Name, true))
                .ToListAsync();

            var defaults = DefaultClasses
                .OrderBy(x => x)
                .Select(x => new ClassOptionDto(0, x, false));

            return Ok(defaults.Concat(custom));
        }

        [HttpPost]
        public async Task<ActionResult<ClassOptionDto>> Add(int campaignId, [FromBody] CharacterClassOption req)
        {
            var userId = GetUserId();
            if (userId <= 0) return Unauthorized();

            var hasAccess = await _db.Campaigns.AnyAsync(c =>
            c.Id == campaignId &&
            (c.OwnerId == userId || c.Players.Any(p => p.UserId == userId)));

            if (!hasAccess) return Forbid();

            var name = (req?.Name ?? "").Trim();
            if (name.Length < 2 || name.Length > 50)
                return BadRequest("Class name must be 2–50 characters.");

            // Don’t allow duplicates vs defaults
            if (DefaultClasses.Any(d => d.Equals(name, StringComparison.OrdinalIgnoreCase)))
                return Conflict("That class already exists in the default list.");

            var normalized = name.ToUpperInvariant();

            var exists = await _db.CharacterClassOptions.AnyAsync(x =>
                x.CampaignId == campaignId && x.NormalizedName == normalized);

            if (exists) return Conflict("You already added that class.");

            var option = new Models.CharacterClassOption
            {
                CampaignId = campaignId,
                UserId = userId,
                Name = name,
                NormalizedName = normalized
            };

            _db.CharacterClassOptions.Add(option);
            await _db.SaveChangesAsync();

            return Ok(new ClassOptionDto(option.Id, option.Name, true));
        }
    }
}

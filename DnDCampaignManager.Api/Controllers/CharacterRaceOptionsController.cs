using DnDCampaignManager.Api.Services;
using DnDCampingManager.Api.Data;
using DnDCampaignManager.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace DnDCampaignManager.Api.Controllers
{
    [ApiController]
    [Route("api/campaigns/{campaignId}/character-races")]
    public class CharacterRaceOptionsController : ControllerBase
    {
        private readonly DnDxDbContext _dnDxDbContext;

        private static readonly string[] DefaultRaces =
        {
            "Dragonborn","Dwarf","Elf","Gnome","Half-Elf","Half-Orc",
            "Halfling","Human","Tiefling"
        };

        public CharacterRaceOptionsController(DnDxDbContext db) => _dnDxDbContext = db;

        private int GetUserId()
        {
            var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(raw, out var id) ? id : 0;
        }

        public record CharacterRaceOptionDto(int Id, string Name, bool IsCustom);
        public record AddRaceRequest(string Name);

        [HttpGet]
        public async Task<ActionResult<IEnumerable<CharacterRaceOptionDto>>> Get(int campaignId)
        {
            var userId = GetUserId();
            if (userId <= 0) return Unauthorized();

            var hasAccess = await CampaignAuthorization.CanAccessAsync(_dnDxDbContext, campaignId, userId);

            if (!hasAccess) return Forbid();

            var custom = await _dnDxDbContext.CharacterRaceOptions
                .Where(x => x.CampaignId == campaignId)
                .OrderBy(x => x.Name)
                .Select(x => new CharacterRaceOptionDto(x.Id, x.Name, true))
                .ToListAsync();

            var defaults = DefaultRaces
                .OrderBy(x => x)
                .Select(x => new CharacterRaceOptionDto(0, x, false));

            return Ok(defaults.Concat(custom));
        }

        [HttpPost]
        public async Task<ActionResult<CharacterRaceOptionDto>> Add(int campaignId, [FromBody] AddRaceRequest req)
        {
            var userId = GetUserId();
            if (userId <= 0) return Unauthorized();

            var hasAccess = await CampaignAuthorization.CanAccessAsync(_dnDxDbContext, campaignId, userId);

            if (!hasAccess) return Forbid();

            var name = (req?.Name ?? "").Trim();
            if (name.Length < 2 || name.Length > 50)
                return BadRequest("Race name must be 2–50 characters.");

            if (DefaultRaces.Any(d => d.Equals(name, StringComparison.OrdinalIgnoreCase)))
                return Conflict("That race already exists in the default list.");

            var normalized = name.ToUpperInvariant();

            var exists = await _dnDxDbContext.CharacterRaceOptions.AnyAsync(x =>
                x.CampaignId == campaignId && x.NormalizedName == normalized);

            if (exists) return Conflict("You already added that race.");

            var option = new CharacterRaceOption
            {
                CampaignId = campaignId,
                UserId = userId,
                Name = name,
                NormalizedName = normalized
            };

            _dnDxDbContext.CharacterRaceOptions.Add(option);
            await _dnDxDbContext.SaveChangesAsync();

            return Ok(new CharacterRaceOptionDto(option.Id, option.Name, true));
        }
    }
}

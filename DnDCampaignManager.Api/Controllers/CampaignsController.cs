using DnDCampaignManager.Api.DTOs;
using DnDCampaignManager.Api.Models;
using DnDCampingManager.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace DnDCampaignManager.Api.Controllers
{
    [ApiController]
    [Route("api/campaigns")]
    public class CampaignsController : ControllerBase
    {
        private readonly DnDxDbContext _dnDxDbContext;

        public CampaignsController(DnDxDbContext db)
        {
            _dnDxDbContext = db;
        }

        private int GetUserId()
        {
            return int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        }

        [HttpGet("{id}")]
        [Authorize(Roles = "DM")]
        public async Task<IActionResult> GetCampaign(int id)
        {
            var userId = GetUserId();

            var campaign = await _dnDxDbContext.Campaigns
                .Where(c => c.Id == id && c.OwnerId == userId)
                .Include(c => c.Players)
                .Select(c => new CampaignResponseDto(
                    c.Id,
                    c.Name,
                    c.Description,
                    c.OwnerId,
                    c.Characters.Select(ch => new CharacterCampaignResponseDto
                    {
                        Id = ch.Id,
                        UserId = ch.UserId,
                        Name = ch.Name,
                        Class = ch.Class,
                        Race = ch.Race,
                        Level = ch.Level
                    }).ToList(),
                    c.Players.Select(p => new PlayerResponseDto(
                        p.UserId,
                        p.User.Email
                        )).ToList()
                ))
                .SingleOrDefaultAsync();

            if (campaign == null)
                return NotFound();

            return Ok(campaign);
        }


        [HttpGet]
        public async Task<ActionResult<List<CampaignResponseDto>>> GetMyCampaigns()
        {
            var userId = GetUserId();

            var partCamp = await _dnDxDbContext.CampaignPlayer.Where(x => x.UserId == userId).ToListAsync();

            var campaigns = await _dnDxDbContext.Campaigns
                .Where(c =>
                    c.OwnerId == userId || partCamp.Select(x => x.CampaignId).ToList().Contains(c.Id)
                )
                .Include(c => c.Players)
                .Include(c => c.Characters)
                .Select(c => new CampaignResponseDto(
                    c.Id,
                    c.Name,
                    c.Description,
                    c.OwnerId,
                    c.Characters.Select(ch => new CharacterCampaignResponseDto
                    {
                        Id = ch.Id,
                        UserId = ch.UserId,
                        Name = ch.Name,
                        Class = ch.Class,
                        Race = ch.Race,
                        Level = ch.Level
                    }).ToList(),
                    c.Players.Select(p => new PlayerResponseDto(
                        p.UserId,
                        p.User.Email
                    )).ToList()
                )).ToListAsync();

            return Ok(campaigns);
        }



        [HttpPost]
        [Authorize(Roles = "DM")]
        public async Task<ActionResult<CampaignResponseDto>> CreateCampaign(CreateCampaignDto Create)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
                return Unauthorized();

            var userId = int.Parse(userIdClaim.Value);

            var campaign = new Campaign
            {
                Name = Create.Name,
                Description = Create.Description,
                OwnerId = userId
            };

            _dnDxDbContext.Campaigns.Add(campaign);
            await _dnDxDbContext.SaveChangesAsync();

            return Ok(new CampaignResponseDto(
                campaign.Id,
                campaign.Name,
                campaign.Description,
                campaign.OwnerId,
                new List<CharacterCampaignResponseDto>(),
                new List<PlayerResponseDto>()
            ));
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "DM")]
        public async Task<IActionResult> UpdateCampaign(int id, [FromBody] UpdateCampaignDto update)
        {
            var userId = GetUserId();

            var campaign = await _dnDxDbContext.Campaigns
                .Include(c => c.Players)
                .ThenInclude(cp => cp.User)
                .ThenInclude(cp => cp.Characters)
                .SingleOrDefaultAsync(c =>
                    c.Id == id &&
                    c.OwnerId == userId
                );

            if (campaign == null)
                return NotFound();

            campaign.Name = update.Name;
            campaign.Description = update.Description;

            await _dnDxDbContext.SaveChangesAsync();

            return Ok(new CampaignResponseDto(
                campaign.Id,
                campaign.Name,
                campaign.Description,
                campaign.OwnerId,
                campaign.Characters.Select(ch => new CharacterCampaignResponseDto
                {
                    Id = ch.Id,
                    UserId = ch.UserId,
                    Name = ch.Name,
                    Class = ch.Class,
                    Race = ch.Race,
                    Level = ch.Level
                }).ToList(),
                campaign.Players.Select(p => new PlayerResponseDto(
                    p.UserId,
                    p.User.Email
                )).ToList()
            ));
        }

        [HttpDelete("{id:int}")]
        [Authorize(Roles = "DM")]
        public async Task<IActionResult> DeleteCampaign(int id)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var campaign = await _dnDxDbContext.Campaigns
                .FirstOrDefaultAsync(c => c.Id == id && c.OwnerId == userId);

            if (campaign == null)
                return NotFound();

            _dnDxDbContext.Campaigns.Remove(campaign);
            await _dnDxDbContext.SaveChangesAsync();

            return NoContent();
        }

        [Authorize(Roles = "DM")]
        [HttpPost("{id}/players")]
        public async Task<IActionResult> AddPlayerToCampaign(int id, AddPlayerDto dto)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var campaign = await _dnDxDbContext.Campaigns
                .Include(c => c.Players)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (campaign == null)
                return NotFound();

            if (campaign.OwnerId != userId)
                return Forbid();

            var player = await _dnDxDbContext.Users
                .SingleOrDefaultAsync(u => u.Email == dto.Email);

            if (player == null)
                return BadRequest("User not found");

            var alreadyAdded = campaign.Players
                .Any(p => p.UserId == player.Id);

            if (alreadyAdded)
                return BadRequest("Player already in campaign");

            campaign.Players.Add(new CampaignPlayer
            {
                CampaignId = campaign.Id,
                UserId = player.Id
            });

            await _dnDxDbContext.SaveChangesAsync();

            return Ok();
        }

        [HttpDelete("{id}/players/{playerId:int}")]
        [Authorize(Roles = "DM")]
        public async Task<IActionResult> RemovePlayerFromCampaign(int id, int playerId)
        {
            var userId = GetUserId();

            var campaign = await _dnDxDbContext.Campaigns
                .Include(c => c.Players)
                .SingleOrDefaultAsync(c =>
                    c.Id == id &&
                    c.OwnerId == userId
                );

            if (campaign == null)
                return NotFound();

            if (playerId == userId)
                return BadRequest("You cannot remove yourself from the campaign.");

            var player = campaign.Players
                .SingleOrDefault(p => p.UserId == playerId);

            if (player == null)
                return NotFound("Player not in campaign");

            _dnDxDbContext.Remove(player);
            await _dnDxDbContext.SaveChangesAsync();

            return NoContent();
        }
    }
}

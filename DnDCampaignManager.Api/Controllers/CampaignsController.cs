using DnDCampaignManager.Api.Services;
﻿using DnDCampaignManager.Api.DTOs;
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

            var campaigns = await _dnDxDbContext.Campaigns
                .AccessibleTo(userId)
                .Include(c => c.Players).ThenInclude(p => p.User)
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
                    c.Players.Select(p => new PlayerResponseDto(p.UserId, p.User.Email)).ToList()
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

            await using var transaction = _dnDxDbContext.Database.CurrentTransaction is null
                ? await _dnDxDbContext.Database.BeginTransactionAsync() : null;
            // Serialize campaign creation with DM demotion so an in-flight request cannot recreate deleted DM data.
            var owner = await _dnDxDbContext.Users.FromSqlInterpolated(
                $"SELECT * FROM \"Users\" WHERE \"Id\" = {userId} FOR UPDATE").SingleOrDefaultAsync();
            if (owner?.Role != Roles.DM) return Forbid();

            var campaign = new Campaign
            {
                Name = Create.Name,
                Description = Create.Description,
                OwnerId = userId
            };

            _dnDxDbContext.Campaigns.Add(campaign);
            await _dnDxDbContext.SaveChangesAsync();
            if (transaction is not null) await transaction.CommitAsync();

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
        [HttpPost("{campaignId}/players")]
        public async Task<IActionResult> AddPlayerToCampaign(int campaignId, AddPlayerDto dto)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var campaign = await _dnDxDbContext.Campaigns
                .Include(c => c.Players)
                .FirstOrDefaultAsync(c => c.Id == campaignId);

            if (campaign == null)
                return NotFound();

            if (campaign.OwnerId != userId)
                return Forbid();

            var normalizedEmail = dto.Email.Trim().ToLowerInvariant();
            var matches = await _dnDxDbContext.Users
                .Where(u => u.NormalizedEmail == normalizedEmail)
                .Take(2)
                .ToListAsync();

            if (matches.Count > 1)
                return Conflict("Multiple accounts match this email. Use a unique account email.");

            var player = matches.SingleOrDefault();

            if (player == null)
                return NotFound("No registered user exists with that email.");

            var alreadyAdded = campaign.Players
                .Any(p => p.UserId == player.Id);

            if (alreadyAdded)
                return Conflict("Player already in campaign");

            if (player.Id == campaign.OwnerId)
                return BadRequest("The campaign DM already has access to this campaign.");

            campaign.Players.Add(new CampaignPlayer
            {
                CampaignId = campaign.Id,
                UserId = player.Id
            });

            await _dnDxDbContext.SaveChangesAsync();

            return Ok(new PlayerResponseDto(player.Id, player.Email));
        }

        [HttpDelete("{campaignId}/players/{playerId:int}")]
        [Authorize(Roles = "DM")]
        public async Task<IActionResult> RemovePlayerFromCampaign(int campaignId, int playerId)
        {
            var userId = GetUserId();

            var campaign = await _dnDxDbContext.Campaigns
                .Include(c => c.Players)
                .SingleOrDefaultAsync(c => c.Id == campaignId && c.OwnerId == userId);

            if (campaign == null) return NotFound();

            if (playerId == userId)
                return BadRequest("You cannot remove yourself from the campaign.");

            var link = campaign.Players.SingleOrDefault(p => p.UserId == playerId);
            if (link == null) return NotFound("Player not in campaign.");

            _dnDxDbContext.CampaignPlayer.Remove(link);
            await _dnDxDbContext.SaveChangesAsync();

            return NoContent();
        }

        [HttpDelete("{campaignId}/characters/{characterId:int}")]
        [Authorize]
        public async Task<IActionResult> DeleteCharacter(int campaignId, int characterId)
        {
            var userId = GetUserId();
            var isDM = User.IsInRole("DM");

            var character = await _dnDxDbContext.Characters
                .SingleOrDefaultAsync(ch => ch.Id == characterId && ch.CampaignId == campaignId);

            if (character == null)
                return NotFound("Character not found in this campaign.");

            if (isDM)
            {
                var ownsCampaign = await _dnDxDbContext.Campaigns.AnyAsync(c =>
                    c.Id == campaignId && c.OwnerId == userId);

                if (!ownsCampaign) return Forbid();
            }
            else
            {
                if (character.UserId != userId || !await _dnDxDbContext.CampaignPlayer
                    .AnyAsync(p => p.CampaignId == campaignId && p.UserId == userId)) return Forbid();
            }

            _dnDxDbContext.Characters.Remove(character);
            await _dnDxDbContext.SaveChangesAsync();

            return NoContent();
        }
    }
}

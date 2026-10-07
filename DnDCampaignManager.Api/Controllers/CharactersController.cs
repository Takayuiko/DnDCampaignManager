using DnDCampaignManager.Api.DTOs;
using DnDCampaignManager.Api.Models;
using DnDCampingManager.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using DnDCampaignManager.Api.Services;

namespace DnDCampaignManager.Api.Controllers
{
    [ApiController]
    [Route("api/campaigns/{campaignId}/characters")]
    [Authorize]
    public class CharactersController : ControllerBase
    {
        private readonly DnDxDbContext _dnDxDbContext;

        public CharactersController(DnDxDbContext db)
        {
            _dnDxDbContext = db;
        }

        [HttpPost]
        public async Task<IActionResult> CreateCharacter(int campaignId, [FromBody] CreateCharacterDto create)
        {
            var userId = GetUserId();
            await using var transaction = _dnDxDbContext.Database.CurrentTransaction is null
                ? await _dnDxDbContext.Database.BeginTransactionAsync() : null;

            var campaign = await _dnDxDbContext.Campaigns
                .FromSqlInterpolated($"SELECT * FROM \"Campaigns\" WHERE \"Id\" = {campaignId} FOR UPDATE")
                .Include(c => c.Players)
                .SingleOrDefaultAsync(c => c.Id == campaignId);

            if (campaign == null)
                return NotFound("Campaign not found");

            if (!CampaignAuthorization.CanAccess(campaign, userId))
                return Forbid();

            var validationError = CharacterValidation.Errors(create).FirstOrDefault();
            if (validationError is not null) return BadRequest(validationError.ErrorMessage);

            if (await _dnDxDbContext.Characters.AnyAsync(x => x.CampaignId == campaignId && x.UserId == userId))
                return BadRequest("You already have a character in this campaign.");
            // The campaign lock serializes simultaneous creation requests at the capacity boundary.
            if (await _dnDxDbContext.Characters.CountAsync(x => x.CampaignId == campaignId) >= 6)
                return Conflict("This campaign already has the maximum of 6 characters.");

            var character = new Character { CampaignId = campaignId, UserId = userId, Campaign = campaign };
            CharacterMapping.Apply(character, create, creating: true);

            _dnDxDbContext.Characters.Add(character);
            await _dnDxDbContext.SaveChangesAsync();
            if (transaction is not null) await transaction.CommitAsync();

            return Ok(CharacterMapping.ToResponse(character));
        }

        [HttpPut("{characterId}")]
        [Authorize]
        public async Task<IActionResult> UpdateCharacter(int campaignId, int characterId, UpdateCharacterDto update)
        {
            var userId = GetUserId();
            var isDM = User.IsInRole("DM");
            await using var transaction = _dnDxDbContext.Database.CurrentTransaction is null
                ? await _dnDxDbContext.Database.BeginTransactionAsync() : null;

            var character = await _dnDxDbContext.Characters
                .FromSqlInterpolated($"SELECT * FROM \"Characters\" WHERE \"Id\" = {characterId} AND \"CampaignId\" = {campaignId} FOR UPDATE")
                .Include(c => c.Campaign)
                .Include(c => c.Skills)
                .Include(c => c.Attacks)
                .FirstOrDefaultAsync(c =>
                    c.Id == characterId &&
                    c.CampaignId == campaignId);

            if (character == null)
                return NotFound();

            var canEdit = await CampaignAuthorization.CanUseCharacterAsync(_dnDxDbContext, character, userId, isDM);
            
            if (!canEdit)
                return Forbid();

            var validationError = CharacterValidation.Errors(update).FirstOrDefault();
            if (validationError is not null) return BadRequest(validationError.ErrorMessage);

            if (update.Version is null)
                return BadRequest("Reload the character before saving; its version is required.");
            if (update.Version != character.Version)
                return CharacterConflict();

            CharacterMapping.Apply(character, update);
            character.Version = Guid.NewGuid();

            try
            {
                await _dnDxDbContext.SaveChangesAsync();
                if (transaction is not null) await transaction.CommitAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                return CharacterConflict();
            }
            return Ok(new CharacterSaveResponseDto(character.Version));
        }

        [HttpGet]
        public async Task<IActionResult> GetCharactersByCampaign(int campaignId)
        {
            var userId = GetUserId();

            var campaign = await CampaignAuthorization.GetAccessAsync(_dnDxDbContext, campaignId, userId);

            if (campaign == null)
                return NotFound();

            var isDM = campaign.OwnerId == userId;

            if (!campaign.CanAccess(userId))
                return Forbid();

            var characters = await _dnDxDbContext.Characters
                .Where(c => c.CampaignId == campaignId && (isDM || c.UserId == userId))
                .OrderBy(c => c.Id)
                .Select(CharacterMapping.ListItem)
                .ToListAsync();

            return Ok(characters);
        }

        [HttpGet("{characterId}")]
        public async Task<IActionResult> GetCharacter(int campaignId, int characterId)
        {
            var userId = GetUserId();
            var isDM = User.IsInRole("DM");

            var character = await _dnDxDbContext.Characters
                .Include(c => c.Campaign)
                .Include(c => c.Skills)
                .Include(c => c.Attacks)
                .SingleOrDefaultAsync(c =>
                    c.Id == characterId &&
                    c.CampaignId == campaignId
                );

            if (character == null)
                return NotFound();

            var canView = await CampaignAuthorization.CanUseCharacterAsync(_dnDxDbContext, character, userId, isDM);

            if (!canView)
                return Forbid();

            return Ok(CharacterMapping.ToResponse(character));
        }

        private ConflictObjectResult CharacterConflict() => Conflict(new
        {
            code = "character_version_conflict",
            error = "This character was changed by someone else. Your edits have not been saved. Reload the latest character before saving again."
        });

        private int GetUserId()
        {
            return int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        }
    }
}

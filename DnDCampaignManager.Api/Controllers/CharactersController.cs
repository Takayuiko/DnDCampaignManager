using DnDCampaignManager.Api.DTOs;
using DnDCampaignManager.Api.Models;
using DnDCampaignManager.Api.Services;
using DnDCampingManager.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

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

            var campaign = await _dnDxDbContext.Campaigns
                .Include(c => c.Players)
                .Include(x => x.Characters)
                .SingleOrDefaultAsync(c => c.Id == campaignId);

            if (campaign == null)
                return NotFound("Campaign not found");

            if (campaign.Characters.Any(x => x.UserId == userId))
            {
                return BadRequest("You already have a character in this campaign.");
            }

            var isOwner = campaign.OwnerId == userId;
            var isPlayer = campaign.Players.Any(p => p.UserId == userId);

            if (!isOwner && !isPlayer)
                return Forbid();

            var character = new Character
            {
                Name = create.Name,
                Class = create.Class,
                Race = create.Race,
                Level = create.Level,
                Background = create.Background,
                Alignment = create.Alignment,
                ExperiencePoints = create.ExperiencePoints,
                Strength = create.Strength,
                Dexterity = create.Dexterity,
                Constitution = create.Constitution,
                Intelligence = create.Intelligence,
                Wisdom = create.Wisdom,
                Charisma = create.Charisma,
                CampaignId = campaignId,
                UserId = userId,
                Campaign = campaign,
                ProficiencyBonus = create.ProficiencyBonus,
                ArmorClass = create.ArmorClass,
                Initiative = create.Initiative,
                Speed = create.Speed,
                HitPointMax = create.HitPointMax,
                HitPointCurrent = create.HitPointCurrent,
                HitPointTemporary = create.HitPointTemporary
            };

            _dnDxDbContext.Characters.Add(character);
            await _dnDxDbContext.SaveChangesAsync();

            _dnDxDbContext.CharacterSkills.AddRange(SkillDefaults.CreateDefaultSkills(character.Id));
            await _dnDxDbContext.SaveChangesAsync();

            return Ok(new CharacterResponseDto
            {
                Id = character.Id,
                UserId = character.UserId,
                Name = character.Name,
                Class = character.Class,
                Race = character.Race,
                Level = character.Level,
                Background = character.Background,
                Alignment = character.Alignment,
                ExperiencePoints = character.ExperiencePoints,
                Strength = character.Strength,
                Dexterity = character.Dexterity,
                Constitution = character.Constitution,
                Intelligence = character.Intelligence,
                Wisdom = character.Wisdom,
                Charisma = character.Charisma,
                ProficiencyBonus = character.ProficiencyBonus,
                ArmorClass = character.ArmorClass,
                Initiative = character.Initiative,
                Speed = character.Speed,
                HitPointMax = character.HitPointMax,
                HitPointCurrent = character.HitPointCurrent,
                HitPointTemporary = character.HitPointTemporary
            });
        }

        [HttpPut("{characterId}")]
        [Authorize]
        public async Task<IActionResult> UpdateCharacter(int campaignId, int characterId, UpdateCharacterDto update)
        {
            var userId = GetUserId();
            var isDM = User.IsInRole("DM");

            var character = await _dnDxDbContext.Characters
                .Include(c => c.Campaign)
                .Include(c => c.Skills)
                .FirstOrDefaultAsync(c =>
                    c.Id == characterId &&
                    c.CampaignId == campaignId);

            if (character == null)
                return NotFound();

            var canEdit = (isDM && character.Campaign.OwnerId == userId) || (!isDM && character.UserId == userId);
            
            if (!canEdit)
                return Forbid();

            character.Name = update.Name;
            character.Class = update.Class;
            character.Race = update.Race;
            character.Level = update.Level;
            character.Background = update.Background;
            character.Alignment = update.Alignment;
            character.ExperiencePoints = update.ExperiencePoints;
            character.Strength = update.Strength;
            character.Dexterity = update.Dexterity;
            character.Constitution = update.Constitution;
            character.Intelligence = update.Intelligence;
            character.Wisdom = update.Wisdom;
            character.Charisma = update.Charisma;
            character.ProficiencyBonus = update.ProficiencyBonus;
            character.ArmorClass = update.ArmorClass;
            character.Initiative = update.Initiative;
            character.Speed = update.Speed;
            character.HitPointMax = update.HitPointMax;
            character.HitPointCurrent = update.HitPointCurrent;
            character.HitPointTemporary = update.HitPointTemporary;

            if (update.Skills != null && update.Skills.Count > 0)
            {
                foreach (var incoming in update.Skills)
                {
                    var existing = character.Skills.SingleOrDefault(s => s.Skill == incoming.Skill);
                    if (existing == null) continue;

                    existing.IsProficient = incoming.IsProficient;
                    existing.IsExpertise = incoming.IsExpertise;
                    existing.MiscBonus = incoming.MiscBonus;
                }
            }

            await _dnDxDbContext.SaveChangesAsync();

            return NoContent();
        }

        [HttpGet]
        public async Task<IActionResult> GetCharactersByCampaign(int campaignId)
        {
            var userId = GetUserId();

            var campaign = await _dnDxDbContext.Campaigns
                .Include(c => c.Players)
                .SingleOrDefaultAsync(c => c.Id == campaignId);

            if (campaign == null)
                return NotFound();

            var isDM = campaign.OwnerId == userId;
            var isPlayer = campaign.Players.Any(p => p.UserId == userId);

            if (!isDM && !isPlayer)
                return Forbid();

            var characters = await _dnDxDbContext.Characters
                .Where(c => c.CampaignId == campaignId).ToListAsync();

            if (!isDM)
            {
                characters = characters.Where(c => c.UserId == userId).ToList();
            }

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
                .SingleOrDefaultAsync(c =>
                    c.Id == characterId &&
                    c.CampaignId == campaignId
                );

            if (character == null)
                return NotFound();

            var canView = (isDM && character.Campaign.OwnerId == userId) || (!isDM && character.UserId == userId);

            if (!canView)
                return Forbid();

            return Ok(new CharacterResponseDto
            {
                Id = character.Id,
                UserId = character.UserId,
                Name = character.Name,
                Class = character.Class,
                Race = character.Race,
                Level = character.Level,
                Background = character.Background,
                Alignment = character.Alignment,
                ExperiencePoints = character.ExperiencePoints,
                Strength = character.Strength,
                Dexterity = character.Dexterity,
                Constitution = character.Constitution,
                Intelligence = character.Intelligence,
                Wisdom = character.Wisdom,
                Charisma = character.Charisma,
                ProficiencyBonus = character.ProficiencyBonus,
                ArmorClass = character.ArmorClass,
                Initiative = character.Initiative,
                Speed = character.Speed,
                HitPointMax = character.HitPointMax,
                HitPointCurrent = character.HitPointCurrent,
                HitPointTemporary = character.HitPointTemporary,
                Skills = character.Skills.OrderBy(s => s.Skill)
                    .Select(s => new CharacterSkillDto
                    {
                        Skill = s.Skill,
                        Ability = s.Ability,
                        IsProficient = s.IsProficient,
                        IsExpertise = s.IsExpertise,
                        MiscBonus = s.MiscBonus
                    }).ToList()
            });
        }

        private int GetUserId()
        {
            return int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        }
    }
}

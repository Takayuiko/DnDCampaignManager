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

            var isOwner = campaign.OwnerId == userId;
            var isPlayer = campaign.Players.Any(p => p.UserId == userId);

            if (!isOwner && !isPlayer)
                return Forbid();

            if (await _dnDxDbContext.Characters.AnyAsync(x => x.CampaignId == campaignId && x.UserId == userId))
                return BadRequest("You already have a character in this campaign.");
            // The campaign lock serializes simultaneous creation requests at the capacity boundary.
            if (await _dnDxDbContext.Characters.CountAsync(x => x.CampaignId == campaignId) >= 6)
                return Conflict("This campaign already has the maximum of 6 characters.");

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
                HitPointTemporary = create.HitPointTemporary,
                Inspiration = create.Inspiration,
            };

            if (create.HitDice is not null)
            {
                character.HitDiceDie = create.HitDice.Die;
                character.HitDiceTotal = create.HitDice.Total;
                character.HitDiceRemaining = create.HitDice.Remaining;
            }

            if (create.Attacks is not null)
            {
                character.Attacks = create.Attacks
                    .Where(a => !string.IsNullOrWhiteSpace(a.Name))
                    .Select(a => new CharacterAttack
                    {
                        Name = a.Name,
                        AttackBonus = a.AttackBonus,
                        Damage = a.Damage
                    })
                    .ToList();
            }

            if (create.Skills != null && create.Skills.Count > 0)
            {
                foreach (var incoming in create.Skills)
                {
                    character.Skills.Add(new CharacterSkill
                    {
                        Skill = incoming.Skill,
                        Ability = incoming.Ability,
                        IsProficient = incoming.IsProficient,
                        IsExpertise = incoming.IsExpertise,
                        MiscBonus = incoming.MiscBonus
                    });
                }
            }

            var st = create.SavingThrows ?? new SavingThrowsDto();

            character.SaveStrProficient = st.Strength.IsProficient;
            character.SaveStrMiscBonus = st.Strength.MiscBonus;

            character.SaveDexProficient = st.Dexterity.IsProficient;
            character.SaveDexMiscBonus = st.Dexterity.MiscBonus;

            character.SaveConProficient = st.Constitution.IsProficient;
            character.SaveConMiscBonus = st.Constitution.MiscBonus;

            character.SaveIntProficient = st.Intelligence.IsProficient;
            character.SaveIntMiscBonus = st.Intelligence.MiscBonus;

            character.SaveWisProficient = st.Wisdom.IsProficient;
            character.SaveWisMiscBonus = st.Wisdom.MiscBonus;

            character.SaveChaProficient = st.Charisma.IsProficient;
            character.SaveChaMiscBonus = st.Charisma.MiscBonus;

            _dnDxDbContext.Characters.Add(character);
            await _dnDxDbContext.SaveChangesAsync();
            if (transaction is not null) await transaction.CommitAsync();

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
                Inspiration = character.Inspiration,
                HitDice = new HitDiceDto(character.HitDiceDie ?? string.Empty, character.HitDiceTotal ?? 0, character.HitDiceRemaining ?? 0),
                Attacks = character.Attacks.Select(a => new CharacterAttackDto
                {
                    Id = a.Id,
                    Name = a.Name,
                    AttackBonus = a.AttackBonus,
                    Damage = a.Damage
                }).ToList(),
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

        [HttpPut("{characterId}")]
        [Authorize]
        public async Task<IActionResult> UpdateCharacter(int campaignId, int characterId, UpdateCharacterDto update)
        {
            var userId = GetUserId();
            var isDM = User.IsInRole("DM");

            var character = await _dnDxDbContext.Characters
                .Include(c => c.Campaign)
                .Include(c => c.Skills)
                .Include(c => c.Attacks)
                .FirstOrDefaultAsync(c =>
                    c.Id == characterId &&
                    c.CampaignId == campaignId);

            if (character == null)
                return NotFound();

            var canEdit = (isDM && character.Campaign.OwnerId == userId) || character.UserId == userId;
            
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
            character.Inspiration = update.Inspiration;

            if (update.HitDice is not null)
            {
                character.HitDiceDie = update.HitDice.Die;
                character.HitDiceTotal = update.HitDice.Total;
                character.HitDiceRemaining = update.HitDice.Remaining;
            }

            if (update.Attacks != null)
            {
                character.Attacks.Clear();
                foreach (var a in update.Attacks)
                {
                    character.Attacks.Add(new CharacterAttack
                    {
                        Name = a.Name,
                        AttackBonus = a.AttackBonus,
                        Damage = a.Damage
                    });
                }
            }

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

            var st = update.SavingThrows ?? new SavingThrowsDto();

            character.SaveStrProficient = st.Strength.IsProficient;
            character.SaveStrMiscBonus = st.Strength.MiscBonus;

            character.SaveDexProficient = st.Dexterity.IsProficient;
            character.SaveDexMiscBonus = st.Dexterity.MiscBonus;

            character.SaveConProficient = st.Constitution.IsProficient;
            character.SaveConMiscBonus = st.Constitution.MiscBonus;

            character.SaveIntProficient = st.Intelligence.IsProficient;
            character.SaveIntMiscBonus = st.Intelligence.MiscBonus;

            character.SaveWisProficient = st.Wisdom.IsProficient;
            character.SaveWisMiscBonus = st.Wisdom.MiscBonus;

            character.SaveChaProficient = st.Charisma.IsProficient;
            character.SaveChaMiscBonus = st.Charisma.MiscBonus;

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
                .Include(c => c.Attacks)
                .SingleOrDefaultAsync(c =>
                    c.Id == characterId &&
                    c.CampaignId == campaignId
                );

            if (character == null)
                return NotFound();

            var canView = (isDM && character.Campaign.OwnerId == userId) || character.UserId == userId;

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
                Inspiration = character.Inspiration,
                HitDice = new HitDiceDto(character.HitDiceDie ?? string.Empty, character.HitDiceTotal ?? 0, character.HitDiceRemaining ?? 0),
                SavingThrows = new SavingThrowsDto
                {
                    Strength = new SavingThrowDto { IsProficient = character.SaveStrProficient, MiscBonus = character.SaveStrMiscBonus },
                    Dexterity = new SavingThrowDto { IsProficient = character.SaveDexProficient, MiscBonus = character.SaveDexMiscBonus },
                    Constitution = new SavingThrowDto { IsProficient = character.SaveConProficient, MiscBonus = character.SaveConMiscBonus },
                    Intelligence = new SavingThrowDto { IsProficient = character.SaveIntProficient, MiscBonus = character.SaveIntMiscBonus },
                    Wisdom = new SavingThrowDto { IsProficient = character.SaveWisProficient, MiscBonus = character.SaveWisMiscBonus },
                    Charisma = new SavingThrowDto { IsProficient = character.SaveChaProficient, MiscBonus = character.SaveChaMiscBonus },
                },
                Attacks = character.Attacks.Select(a => new CharacterAttackDto
                {
                    Id = a.Id,
                    Name = a.Name,
                    AttackBonus = a.AttackBonus,
                    Damage = a.Damage
                }).ToList(),
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

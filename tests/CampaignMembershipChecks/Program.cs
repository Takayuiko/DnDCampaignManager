using System.Security.Claims;
using System.Text.Json;
using DnDCampaignManager.Api.Controllers;
using DnDCampaignManager.Api.DTOs;
using DnDCampaignManager.Api.Models;
using DnDCampingManager.Api.Data;
using DnDCampingManager.Api.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Routing;
using DnDCampaignManager.Api.Models.AI;
using DnDCampaignManager.Api.Services.AI;
using DnDCampaignManager.Api.DTOs.AI_DTO;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text;
using DnDCampaignManager.Api.Services;
using DnDCampingManager.Api.Services;
using DnDCampingManager.Api.Options;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using SkiaSharp;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;

// Exercise MVC validation, which direct controller calls bypass.
var services = new ServiceCollection();
services.AddLogging();
services.AddControllers().AddApplicationPart(typeof(CampaignsController).Assembly);
using var provider = services.BuildServiceProvider();
var validator = provider.GetRequiredService<IObjectModelValidator>();
foreach (var email in new[] { "player@example.test", "invalid-email", "" })
{
    var context = new ActionContext(new DefaultHttpContext { RequestServices = provider },
        new RouteData(), new ActionDescriptor(), new ModelStateDictionary());
    validator.Validate(context, null, "", new AddPlayerDto(email));
    if (context.ModelState.IsValid != (email == "player@example.test"))
        throw new Exception("Invite email validation did not match expectations.");
}
Console.WriteLine("PASS: MVC record validation accepts valid email and rejects invalid or missing email");
foreach (var noteRequest in new[]
{
    new CreateSessionNoteRequest(1, "Session", "The party found a key.", new DateOnly(2026, 10, 4)),
    new CreateSessionNoteRequest(0, "", new string('x', 240001), new DateOnly(2026, 10, 4))
})
{
    var actionContext = new ActionContext(new DefaultHttpContext { RequestServices = provider },
        new RouteData(), new ActionDescriptor(), new ModelStateDictionary());
    validator.Validate(actionContext, null, "", noteRequest);
    if (actionContext.ModelState.IsValid != (noteRequest.SessionNumber == 1))
        throw new Exception("Session-note record validation did not match expectations.");
}
Console.WriteLine("PASS: MVC session-note validation accepts valid input and rejects missing fields, invalid session number and oversized notes");

foreach (var (input, valid) in new (object, bool)[]
{
    (new SaveItemRequest("Torch", "Gear", "", null, 0.01m), true),
    (new SaveItemRequest(" ", "Gear", "", null, null), false),
    (new SaveItemRequest("Torch", "", "", -1, -1), false),
    (new SaveItemRequest(new string('x', 121), "Gear", "", null, null), false),
    (new AssignItemRequest(1, int.MaxValue, ""), true),
    (new AssignItemRequest(0, 0, ""), false),
    (new AssignItemRequest(1, 1, new string('x', 2001)), false)
})
{
    var context = new ActionContext(new DefaultHttpContext { RequestServices = provider },
        new RouteData(), new ActionDescriptor(), new ModelStateDictionary());
    validator.Validate(context, null, "", input);
    if (context.ModelState.IsValid != valid) throw new Exception("Item MVC validation failed.");
}
Console.WriteLine("PASS: MVC item validation enforces names, lengths, nonnegative values and positive quantities");

var apiDirectory = Path.GetFullPath(args[0]);
var configuration = new ConfigurationBuilder()
    .SetBasePath(apiDirectory)
    .AddJsonFile("appsettings.json")
    .AddJsonFile("appsettings.Development.json", optional: true)
    .AddUserSecrets(typeof(DnDxDbContext).Assembly, optional: true)
    .AddEnvironmentVariables()
    .Build();
var sqlCommands = new List<string>();
var options = new DbContextOptionsBuilder<DnDxDbContext>()
    .UseNpgsql(configuration.GetConnectionString("DefaultConnection"))
    .AddInterceptors(new SqlRecorder(sqlCommands)).Options;
await using var db = new DnDxDbContext(options);
var classOptionsBeforeMigration = (await db.Database.GetAppliedMigrationsAsync()).Any()
    ? await db.CharacterClassOptions.AsNoTracking().OrderBy(c => c.Id)
        .Select(c => new { c.Id, c.UserId, c.CampaignId, c.Name, c.NormalizedName, c.CreatedAt }).ToListAsync()
    : [];
await db.Database.MigrateAsync();
var classOptionsAfterMigration = await db.CharacterClassOptions.AsNoTracking().OrderBy(c => c.Id)
    .Select(c => new { c.Id, c.UserId, c.CampaignId, c.Name, c.NormalizedName, c.CreatedAt }).ToListAsync();
if (!classOptionsBeforeMigration.SequenceEqual(classOptionsAfterMigration))
    throw new Exception("Relationship migration changed existing class options or their ownership.");
Console.WriteLine("PASS: Relationship migration preserves existing class options and ownership");
var classModel = db.Model.FindEntityType(typeof(CharacterClassOption))!;
var classUserRelationships = classModel.GetForeignKeys().Where(f => f.PrincipalEntityType.ClrType == typeof(User)).ToList();
if (classUserRelationships.Count != 1 || classUserRelationships[0].Properties.Single().Name != "UserId" ||
    classUserRelationships[0].PrincipalToDependent?.Name != nameof(User.CharacterClassOptions) ||
    classModel.FindProperty("UserId1") is not null)
    throw new Exception("Character class options must have exactly one user relationship through UserId.");
Console.WriteLine("PASS: Class options use one explicit user relationship without a shadow foreign key");
if (await db.Database.SqlQueryRaw<int>("""
    SELECT count(*)::int AS "Value" FROM information_schema.columns
    WHERE table_schema = current_schema() AND table_name = 'CharacterClassOptions' AND column_name = 'UserId1'
    """).SingleAsync() != 0)
    throw new Exception("Relationship migration did not remove the redundant database column.");
Console.WriteLine("PASS: The redundant UserId1 column is removed from the database");
await using var transaction = await db.Database.BeginTransactionAsync();
// All fixtures and membership/character changes are rolled back, even on failure.
var suffix = Guid.NewGuid().ToString("N");
var dm = new User { Email = $"dm-{suffix}@example.test", Role = Roles.DM, PasswordHash = "unused-test-fixture" };
var player = new User { Email = $"player-{suffix}@example.test", Role = Roles.Player, PasswordHash = "unused-test-fixture" };
var other = new User { Email = $"other-{suffix}@example.test", Role = Roles.Player, PasswordHash = "unused-test-fixture" };
db.Users.AddRange(dm, player, other);
await db.SaveChangesAsync();
var first = new Campaign { Name = "Membership check A", Description = "Temporary", OwnerId = dm.Id };
var second = new Campaign { Name = "Membership check B", Description = "Temporary", OwnerId = dm.Id };
db.Campaigns.AddRange(first, second);
await db.SaveChangesAsync();

ControllerContext Context(User user) => new()
{
    HttpContext = new DefaultHttpContext
    {
        User = new ClaimsPrincipal(new ClaimsIdentity(new[] {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Role, user.Role)
        }, "Test"))
    }
};
void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
    Console.WriteLine($"PASS: {message}");
}

var campaigns = new CampaignsController(db) { ControllerContext = Context(dm) };
Check(await campaigns.AddPlayerToCampaign(first.Id, new AddPlayerDto($"missing-{suffix}@example.test")) is NotFoundResult or NotFoundObjectResult, "Unknown user is rejected");
var invite = await campaigns.AddPlayerToCampaign(first.Id, new AddPlayerDto($" {player.Email.ToUpperInvariant()} "));
Check(invite is OkObjectResult { Value: PlayerResponseDto }, "Existing user is added with casing and whitespace handled");
Check(await db.CampaignPlayer.AnyAsync(x => x.CampaignId == first.Id && x.UserId == player.Id), "Membership is persisted");
Check(await campaigns.AddPlayerToCampaign(first.Id, new AddPlayerDto(player.Email)) is ConflictObjectResult, "Duplicate membership is rejected");
await campaigns.AddPlayerToCampaign(second.Id, new AddPlayerDto(player.Email));
await campaigns.AddPlayerToCampaign(first.Id, new AddPlayerDto(other.Email));
campaigns.ControllerContext = Context(player);
var visible = (await campaigns.GetMyCampaigns()).Result as OkObjectResult;
Check(visible?.Value is List<CampaignResponseDto> list && list.Any(x => x.Id == first.Id) && list.Any(x => x.Id == second.Id), "Invited user can see both campaigns");
Check(await campaigns.AddPlayerToCampaign(first.Id, new AddPlayerDto(dm.Email)) is ForbidResult, "A non-owner cannot add members");

var request = JsonSerializer.Deserialize<CreateCharacterDto>("""
{"Name":"Freya","Class":"Fighter","Race":"Human","Level":1,"Background":"Soldier","Alignment":"Neutral","Strength":10,"Dexterity":10,"Constitution":10,"Intelligence":10,"Wisdom":10,"Charisma":10,"ProficiencyBonus":2,"HitPointMax":10,"HitPointCurrent":10,"Skills":[],"SavingThrows":{}}
""")!;
var characters = new CharactersController(db) { ControllerContext = Context(player) };
Check(await characters.CreateCharacter(first.Id, request) is OkObjectResult, "Invited player can create a character");
foreach (var input in new ICharacterWriteDto[] {
    request with { Skills = [new CharacterSkillDto { Skill = (SkillType)999 }] },
    request with { Attacks = [null!] },
    request with { SavingThrows = new SavingThrowsDto { Strength = null! } }
})
{
    var context = new ActionContext(new DefaultHttpContext { RequestServices = provider },
        new RouteData(), new ActionDescriptor(), new ModelStateDictionary());
    validator.Validate(context, null, "", input);
    Check(!context.ModelState.IsValid, "Shared character validation rejects malformed nested input through MVC");
}
var mappedCharacter = new Character();
CharacterMapping.Apply(mappedCharacter, request);
Check(CharacterMapping.ToResponse(mappedCharacter).SavingThrows is not null && mappedCharacter.Skills.Count == 18,
    "Shared character mapping includes saving throws and complete skills");
Check(await db.CharacterSkills.CountAsync(s => s.Character.CampaignId == first.Id && s.Character.UserId == player.Id) == 18,
    "Creating a character with no supplied skills persists all 18 defaults");
Check(await characters.CreateCharacter(first.Id, request) is BadRequestObjectResult, "One character per user per campaign is enforced");
Check(await characters.CreateCharacter(second.Id, request with { Skills = null }) is OkObjectResult, "Same user can create a different character in another campaign");
characters.ControllerContext = Context(other);
Check(await characters.CreateCharacter(first.Id, request with { Skills = [new CharacterSkillDto {
    Skill = SkillType.Arcana, Ability = AbilityType.Intelligence, IsProficient = true, IsExpertise = true, MiscBonus = 3 }] }) is OkObjectResult,
    "One campaign supports multiple players' characters");
Check(await db.CharacterSkills.CountAsync(s => s.Character.CampaignId == first.Id && s.Character.UserId == other.Id) == 18 &&
    await db.CharacterSkills.AnyAsync(s => s.Character.CampaignId == first.Id && s.Character.UserId == other.Id &&
        s.Skill == SkillType.Arcana && s.IsProficient && s.IsExpertise && s.MiscBonus == 3),
    "Partial creation skills overlay defaults without losing supplied proficiency, expertise or bonuses");
var editedCharacter = await db.Characters.SingleAsync(c => c.CampaignId == first.Id && c.UserId == player.Id);
characters.ControllerContext = Context(dm);
var characterList = await characters.GetCharactersByCampaign(first.Id) as OkObjectResult;
Check(characterList?.Value is List<CharacterListItemDto> characterDtos && characterDtos.Count == 2 &&
    characterDtos.Any(c => c.Id == editedCharacter.Id && c.Name == editedCharacter.Name &&
        c.UserId == player.Id && c.Strength == editedCharacter.Strength && c.HitPointCurrent == editedCharacter.HitPointCurrent),
    "Campaign owner receives all characters as list DTOs with identity and sheet values");
using (var characterJson = JsonDocument.Parse(JsonSerializer.Serialize(characterList!.Value,
    new JsonSerializerOptions(JsonSerializerDefaults.Web))))
{
    var fields = characterJson.RootElement[0].EnumerateObject().Select(p => p.Name).ToHashSet();
    Check(characterJson.RootElement.GetArrayLength() == 2 && fields.Count == 22 &&
        fields.Contains("id") && fields.Contains("userId") && fields.Contains("hitPointCurrent") &&
        !fields.Contains("campaign") && !fields.Contains("user"),
        "Character list serializes without entity cycles or account/campaign navigation properties");
}
characters.ControllerContext = Context(player);
Check((await characters.GetCharactersByCampaign(first.Id) as OkObjectResult)?.Value is List<CharacterListItemDto> ownCharacters &&
    ownCharacters.Count == 1 && ownCharacters[0].Id == editedCharacter.Id,
    "Character list only exposes the active player's own character");
characters.ControllerContext = Context(other);
Check(await characters.GetCharactersByCampaign(second.Id) is ForbidResult,
    "Nonmembers cannot list a campaign's characters");
Check(await characters.GetCharactersByCampaign(int.MaxValue) is NotFoundResult,
    "Character list returns not found for a missing campaign");
characters.ControllerContext = Context(dm);
var emptyCharacterCampaign = new Campaign { Name = "Empty character list", Description = "Temporary", OwnerId = dm.Id };
db.Campaigns.Add(emptyCharacterCampaign);
await db.SaveChangesAsync();
Check((await characters.GetCharactersByCampaign(emptyCharacterCampaign.Id) as OkObjectResult)?.Value is List<CharacterListItemDto> emptyCharacters &&
    emptyCharacters.Count == 0, "Authorized empty campaign returns an empty character DTO list");
characters.ControllerContext = Context(other);
var itemService = new ItemService(db);
async Task<bool> ItemRejected(Func<Task> action, int status)
{
    try { await action(); return false; }
    catch (ItemException ex) { return ex.Status == status; }
}
var itemRequest = new SaveItemRequest("  Moonstone  ", "Treasure", "A pale gem.", 0.1m, 50m);
var moonstone = await itemService.SaveAsync(first.Id, null, dm.Id, true, itemRequest, CancellationToken.None);
Check(moonstone.Name == "Moonstone", "DM creates a campaign item with normalized name");
Check(await ItemRejected(() => itemService.SaveAsync(first.Id, null, dm.Id, true,
    itemRequest with { Name = "moonSTONE" }, CancellationToken.None), 409), "Duplicate item names are rejected");
Check(await ItemRejected(() => itemService.SaveAsync(first.Id, null, player.Id, false, itemRequest, CancellationToken.None), 403),
    "Player cannot create catalog items");
Check(await ItemRejected(() => itemService.SaveAsync(first.Id, moonstone.Id, other.Id, true, itemRequest, CancellationToken.None), 403),
    "Another DM cannot edit the campaign catalog");
var assignment = await itemService.AssignAsync(first.Id, editedCharacter.Id, dm.Id, true,
    new AssignItemRequest(moonstone.Id, 3, "Found in the ruins."), CancellationToken.None);
await itemService.AssignAsync(first.Id, editedCharacter.Id, dm.Id, true,
    new AssignItemRequest(moonstone.Id, 1, "A separate gift."), CancellationToken.None);
Check(assignment.Id > 0 && (await itemService.InventoryAsync(first.Id, editedCharacter.Id, player.Id, false, CancellationToken.None)).Items.Count == 2,
    "Assignments persist quantities and separate copies; player reads own inventory");
Check(await ItemRejected(() => itemService.InventoryAsync(first.Id, editedCharacter.Id, other.Id, false, CancellationToken.None), 403),
    "Other players cannot read a character inventory");
Check(await ItemRejected(() => itemService.AssignAsync(first.Id, editedCharacter.Id, player.Id, false,
    new AssignItemRequest(moonstone.Id, 1, ""), CancellationToken.None), 403), "Player cannot assign items");
var secondCharacter = await db.Characters.SingleAsync(c => c.CampaignId == second.Id && c.UserId == player.Id);
Check(await ItemRejected(() => itemService.AssignAsync(second.Id, secondCharacter.Id, dm.Id, true,
    new AssignItemRequest(moonstone.Id, 1, ""), CancellationToken.None), 404), "Cross-campaign item assignment is rejected");
Check(await ItemRejected(() => itemService.AssignAsync(first.Id, secondCharacter.Id, dm.Id, true,
    new AssignItemRequest(moonstone.Id, 1, ""), CancellationToken.None), 404), "Cross-campaign character assignment is rejected");
Check(await ItemRejected(() => itemService.DeleteAsync(first.Id, moonstone.Id, dm.Id, true, CancellationToken.None), 409),
    "Assigned catalog items cannot be deleted");
await itemService.SaveAsync(first.Id, moonstone.Id, dm.Id, true, itemRequest with { Description = "Updated gem." }, CancellationToken.None);
Check((await itemService.InventoryAsync(first.Id, editedCharacter.Id, dm.Id, true, CancellationToken.None)).Items.All(x => x.Item.Description == "Updated gem."),
    "Catalog edits appear in assigned inventories");
var imported = await itemService.ImportAsync(first.Id, dm.Id, true, CancellationToken.None);
Check(imported == new ImportItemsResult(74, 0) &&
    await itemService.ImportAsync(first.Id, dm.Id, true, CancellationToken.None) == new ImportItemsResult(0, 0),
    "SRD starter import is repeatable without duplicating items");
var torch = await db.CampaignItems.SingleAsync(x => x.CampaignId == first.Id && x.Name == "Torch");
var dart = await db.CampaignItems.SingleAsync(x => x.CampaignId == first.Id && x.Name == "Dart");
var plate = await db.CampaignItems.SingleAsync(x => x.CampaignId == first.Id && x.Name == "Plate");
var bell = await db.CampaignItems.SingleAsync(x => x.CampaignId == first.Id && x.Name == "Bell");
Check(torch.CostGp == 0.01m && torch.WeightLb == 1 && dart.CostGp == 0.05m && dart.WeightLb == 0.25m &&
    plate.CostGp == 1500 && plate.WeightLb == 65 && bell.WeightLb == 0,
    "SRD imports preserve copper prices, fractional weights, armor values and negligible weight");
torch.WeightLb = null;
torch.CostGp = 7;
torch.Description = "DM's custom torch.";
moonstone.WeightLb = null;
moonstone.CostGp = null;
var customSling = await itemService.SaveAsync(second.Id, null, dm.Id, true,
    new SaveItemRequest("Sling", "Homebrew", "Custom sling.", null, null), CancellationToken.None);
await db.SaveChangesAsync();
Check(await itemService.ImportAsync(first.Id, dm.Id, true, CancellationToken.None) == new ImportItemsResult(0, 1) &&
    torch.WeightLb == 1 && torch.CostGp == 7 && torch.Description == "DM's custom torch." && moonstone.WeightLb == null,
    "Reimport fills only missing SRD values and preserves DM values and custom items");
await itemService.ImportAsync(second.Id, dm.Id, true, CancellationToken.None);
Check(customSling.CostGp == null && customSling.WeightLb == null && customSling.Category == "Homebrew",
    "Reimport does not backfill custom items with matching starter names");
Check(await itemService.ImportAsync(first.Id, dm.Id, true, CancellationToken.None) == new ImportItemsResult(0, 0),
    "Reimport after backfill is idempotent");
var disposable = await itemService.SaveAsync(second.Id, null, dm.Id, true,
    itemRequest with { Name = "Disposable" }, CancellationToken.None);
await itemService.DeleteAsync(second.Id, disposable.Id, dm.Id, true, CancellationToken.None);
Check(!await db.CampaignItems.AnyAsync(x => x.Id == disposable.Id), "DM deletes an unassigned item");
Check(!(await itemService.ListAsync(first.Id, player.Id, false, CancellationToken.None)).CanManage,
    "Player catalog access exposes no management permission");
Check(await ItemRejected(() => itemService.ListAsync(second.Id, other.Id, false, CancellationToken.None), 403),
    "Nonmembers cannot read another campaign's catalog");
var cleanupCampaign = new Campaign { Name = "Inventory cleanup", Description = "Temporary", OwnerId = dm.Id };
db.Campaigns.Add(cleanupCampaign);
await db.SaveChangesAsync();
var cleanupCharacter = new Character { CampaignId = cleanupCampaign.Id, UserId = player.Id,
    Name = "Cleanup", Class = "Fighter", Race = "Human", Background = "Soldier", Alignment = "Neutral" };
db.Characters.Add(cleanupCharacter);
await db.SaveChangesAsync();
var cleanupItem = await itemService.SaveAsync(cleanupCampaign.Id, null, dm.Id, true, itemRequest, CancellationToken.None);
await itemService.AssignAsync(cleanupCampaign.Id, cleanupCharacter.Id, dm.Id, true,
    new AssignItemRequest(cleanupItem.Id, 1, ""), CancellationToken.None);
await db.Campaigns.Where(x => x.Id == cleanupCampaign.Id).ExecuteDeleteAsync();
Check(!await db.CharacterItems.AnyAsync(x => x.CampaignId == cleanupCampaign.Id) &&
    !await db.CampaignItems.AnyAsync(x => x.CampaignId == cleanupCampaign.Id),
    "Campaign deletion cascades catalog and inventory without foreign-key errors");
await itemService.AssignAsync(second.Id, secondCharacter.Id, dm.Id, true,
    new AssignItemRequest(customSling.Id, 2, "Keep this inventory."), CancellationToken.None);
var deletePreview = await itemService.DeleteAllPreviewAsync(first.Id, dm.Id, true, CancellationToken.None);
Check(deletePreview.ItemCount > 0 && deletePreview.InventoryEntryCount == 2,
    "Delete-all preview counts catalog items and every character inventory entry");
Check(await ItemRejected(() => itemService.DeleteAllPreviewAsync(first.Id, player.Id, false, CancellationToken.None), 403) &&
    await ItemRejected(() => itemService.DeleteAllAsync(first.Id, other.Id, true,
        new DeleteAllItemsRequest(deletePreview.ItemCount, deletePreview.InventoryEntryCount), CancellationToken.None), 403),
    "Players and unrelated DMs cannot review or execute bulk item deletion");
await itemService.SaveAsync(first.Id, null, dm.Id, true, itemRequest with { Name = "New item after preview" }, CancellationToken.None);
Check(await ItemRejected(() => itemService.DeleteAllAsync(first.Id, dm.Id, true,
    new DeleteAllItemsRequest(deletePreview.ItemCount, deletePreview.InventoryEntryCount), CancellationToken.None), 409) &&
    await db.CharacterItems.CountAsync(x => x.CampaignId == first.Id) == 2,
    "Stale bulk-deletion confirmation leaves all inventory entries intact");
deletePreview = await itemService.DeleteAllPreviewAsync(first.Id, dm.Id, true, CancellationToken.None);
var deletedItems = await itemService.DeleteAllAsync(first.Id, dm.Id, true,
    new DeleteAllItemsRequest(deletePreview.ItemCount, deletePreview.InventoryEntryCount), CancellationToken.None);
Check(deletedItems == deletePreview && !await db.CampaignItems.AnyAsync(x => x.CampaignId == first.Id) &&
    !await db.CharacterItems.AnyAsync(x => x.CampaignId == first.Id) &&
    await db.CampaignItems.AnyAsync(x => x.CampaignId == second.Id) &&
    await db.CharacterItems.AnyAsync(x => x.CampaignId == second.Id),
    "Confirmed bulk deletion clears catalog and inventories only in the selected campaign");

var capacityCampaign = new Campaign { Name = "Capacity check", Description = "Temporary", OwnerId = dm.Id };
db.Campaigns.Add(capacityCampaign);
var capacityPlayers = Enumerable.Range(0, 7).Select(i => new User {
    Email = $"capacity-{i}-{suffix}@example.test", Role = Roles.Player, PasswordHash = "unused-test-fixture"
}).ToArray();
db.Users.AddRange(capacityPlayers);
await db.SaveChangesAsync();
db.CampaignPlayer.AddRange(capacityPlayers.Select(p => new CampaignPlayer { CampaignId = capacityCampaign.Id, UserId = p.Id }));
await db.SaveChangesAsync();
for (var i = 0; i < 6; i++)
{
    characters.ControllerContext = Context(capacityPlayers[i]);
    Check(await characters.CreateCharacter(capacityCampaign.Id, request) is OkObjectResult,
        $"Campaign accepts character {i + 1} within its six-character limit");
}
characters.ControllerContext = Context(capacityPlayers[6]);
Check(await characters.CreateCharacter(capacityCampaign.Id, request) is ConflictObjectResult &&
    await db.Characters.CountAsync(x => x.CampaignId == capacityCampaign.Id) == 6,
    "Direct API requests cannot create a seventh character");
characters.ControllerContext = Context(dm);
Check(await characters.CreateCharacter(capacityCampaign.Id, request) is ConflictObjectResult,
    "The six-character limit also applies to the campaign owner");
var hpUpdate = JsonSerializer.Deserialize<UpdateCharacterDto>("""
{"Name":"Freya","Class":"Fighter","Race":"Human","Level":1,"Background":"Soldier","Alignment":"Neutral","Strength":10,"Dexterity":10,"Constitution":10,"Intelligence":10,"Wisdom":10,"Charisma":10,"ProficiencyBonus":2,"HitPointMax":10,"HitPointCurrent":7,"Skills":[],"SavingThrows":{}}
""")!;
characters.ControllerContext = Context(player);
var retainedSkill = editedCharacter.Skills.Single(s => s.Skill == SkillType.Arcana);
retainedSkill.IsProficient = true;
retainedSkill.IsExpertise = true;
retainedSkill.MiscBonus = 4;
db.CharacterSkills.RemoveRange(editedCharacter.Skills.Where(s => s.Skill != SkillType.Arcana).ToList());
await db.SaveChangesAsync();
var legacyCharacter = (CharacterResponseDto)((OkObjectResult)await characters.GetCharacter(first.Id, editedCharacter.Id)).Value!;
Check(legacyCharacter.Skills.Count == 18 && legacyCharacter.Skills.Single(s => s.Skill == SkillType.Arcana).MiscBonus == 4 &&
    await db.CharacterSkills.CountAsync(s => s.CharacterId == editedCharacter.Id) == 1,
    "Reading a partial legacy character exposes editable defaults without modifying stored skills");
var skillUpdate = hpUpdate with { Skills = [new CharacterSkillDto {
    Skill = SkillType.Stealth, Ability = AbilityType.Dexterity, IsProficient = true, IsExpertise = true, MiscBonus = 2 }] };
Check(await characters.UpdateCharacter(first.Id, editedCharacter.Id, skillUpdate) is NoContentResult &&
    await db.CharacterSkills.CountAsync(s => s.CharacterId == editedCharacter.Id) == 18 &&
    await db.CharacterSkills.AnyAsync(s => s.CharacterId == editedCharacter.Id && s.Skill == SkillType.Stealth &&
        s.Ability == AbilityType.Dexterity && s.IsProficient && s.IsExpertise && s.MiscBonus == 2) &&
    await db.CharacterSkills.AnyAsync(s => s.CharacterId == editedCharacter.Id && s.Skill == SkillType.Arcana &&
        s.IsProficient && s.IsExpertise && s.MiscBonus == 4),
    "Saving a missing legacy skill persists its values, fills gaps and preserves existing skill values");
Check(await characters.UpdateCharacter(first.Id, editedCharacter.Id, skillUpdate) is NoContentResult &&
    await db.CharacterSkills.CountAsync(s => s.CharacterId == editedCharacter.Id) == 18,
    "Repeated skill saves do not create duplicate rows");
Check(await characters.UpdateCharacter(first.Id, editedCharacter.Id, hpUpdate with { Skills = null }) is NoContentResult &&
    await db.CharacterSkills.AnyAsync(s => s.CharacterId == editedCharacter.Id && s.Skill == SkillType.Stealth && s.MiscBonus == 2),
    "Updates with omitted skills preserve existing skill values");
Check(await characters.UpdateCharacter(first.Id, editedCharacter.Id, hpUpdate with {
    Skills = [skillUpdate.Skills![0], skillUpdate.Skills[0]] }) is BadRequestObjectResult &&
    await characters.UpdateCharacter(first.Id, editedCharacter.Id, hpUpdate with {
        Skills = [new CharacterSkillDto { Skill = (SkillType)999, Ability = AbilityType.Wisdom }] }) is BadRequestObjectResult,
    "Duplicate and undefined skills are rejected before changing the character");
var emptySkillsCharacter = await db.Characters.Include(c => c.Skills)
    .SingleAsync(c => c.CampaignId == second.Id && c.UserId == player.Id);
db.CharacterSkills.RemoveRange(emptySkillsCharacter.Skills.ToList());
await db.SaveChangesAsync();
var emptySkillsResponse = (CharacterResponseDto)((OkObjectResult)await characters.GetCharacter(second.Id, emptySkillsCharacter.Id)).Value!;
Check(emptySkillsResponse.Skills.Count == 18 &&
    await db.CharacterSkills.CountAsync(s => s.CharacterId == emptySkillsCharacter.Id) == 0 &&
    await characters.UpdateCharacter(second.Id, emptySkillsCharacter.Id, skillUpdate) is NoContentResult &&
    await db.CharacterSkills.CountAsync(s => s.CharacterId == emptySkillsCharacter.Id) == 18 &&
    await db.CharacterSkills.AnyAsync(s => s.CharacterId == emptySkillsCharacter.Id && s.Skill == SkillType.Stealth &&
        s.IsProficient && s.IsExpertise && s.MiscBonus == 2),
    "A legacy character with zero stored skills can display and persist its first skill edits");
Check(await characters.GetCharacter(first.Id, editedCharacter.Id) is OkObjectResult,
    "Active player can read their own character");
Check(await characters.UpdateCharacter(first.Id, editedCharacter.Id, hpUpdate) is NoContentResult && editedCharacter.HitPointCurrent == 7,
    "Player can save their own character HP");
var promotedCharacterOwner = Context(player);
((ClaimsIdentity)promotedCharacterOwner.HttpContext.User.Identity!).AddClaim(new Claim(ClaimTypes.Role, Roles.DM));
((ClaimsIdentity)promotedCharacterOwner.HttpContext.User.Identity!).AddClaim(new Claim(ClaimTypes.Role, Roles.Admin));
characters.ControllerContext = promotedCharacterOwner;
Check(await characters.UpdateCharacter(first.Id, editedCharacter.Id, hpUpdate with { HitPointCurrent = 6 }) is NoContentResult && editedCharacter.HitPointCurrent == 6,
    "DM/admin can save their own character in another DM's campaign");
characters.ControllerContext = Context(dm);
Check(await characters.GetCharacter(first.Id, editedCharacter.Id) is OkObjectResult,
    "Campaign DM can read a player's character");
Check(await characters.UpdateCharacter(first.Id, editedCharacter.Id, hpUpdate with { HitPointCurrent = 5 }) is NoContentResult && editedCharacter.HitPointCurrent == 5,
    "Campaign DM can save a player's character HP");
characters.ControllerContext = Context(other);
Check(await characters.UpdateCharacter(first.Id, editedCharacter.Id, hpUpdate) is ForbidResult && editedCharacter.HitPointCurrent == 5,
    "Another player cannot edit the character");
((ClaimsIdentity)characters.User.Identity!).AddClaim(new Claim(ClaimTypes.Role, Roles.DM));
((ClaimsIdentity)characters.User.Identity!).AddClaim(new Claim(ClaimTypes.Role, Roles.Admin));
Check(await characters.UpdateCharacter(first.Id, editedCharacter.Id, hpUpdate) is ForbidResult && editedCharacter.HitPointCurrent == 5,
    "Unrelated DM/admin cannot edit the character");
Check(await characters.UpdateCharacter(second.Id, editedCharacter.Id, hpUpdate) is NotFoundResult,
    "Character editing rejects a mismatched campaign route");
var aiConversation = new AIConversation { Id = Guid.NewGuid(), UserId = player.Id, CampaignId = first.Id };
db.AIConversations.Add(aiConversation);
await db.SaveChangesAsync();
var chatContext = Context(player);
using var sseOutput = new MemoryStream();
chatContext.HttpContext.Response.Body = sseOutput;
var fakeEmbeddings = new FakeEmbeddings();
var knowledge = new CampaignKnowledgeService(db, fakeEmbeddings, configuration, NullLogger<CampaignKnowledgeService>.Instance);
var fakeAi = new FakeAIService();
var chat = new AIChatController(db, fakeAi, configuration, NullLogger<AIChatController>.Instance, knowledge)
    { ControllerContext = chatContext };
var defaultResult = (await chat.GetOrCreateDefaultConversation(CancellationToken.None)).Result as OkObjectResult;
var repeatedResult = (await chat.GetOrCreateDefaultConversation(CancellationToken.None)).Result as OkObjectResult;
Check(defaultResult?.Value is ConversationSummaryDto original && repeatedResult?.Value is ConversationSummaryDto repeated &&
    original.Id == repeated.Id && await db.AIConversations.CountAsync(x => x.UserId == player.Id) == 1,
    "Repeated default initialization keeps exactly one conversation");
await chat.StreamMessage(aiConversation.Id, new SendMessageRequest("Test"), CancellationToken.None);
var frames = Encoding.UTF8.GetString(sseOutput.ToArray()).Split("\n\n");
var completionFrame = frames.Single(x => x.StartsWith("event: done\n"));
using var completionJson = JsonDocument.Parse(completionFrame.Split("data: ")[1]);
var completionPayload = completionJson.RootElement;
Check(completionPayload.GetProperty("usage").GetProperty("inputTokens").GetInt64() == 100 &&
    completionPayload.GetProperty("usage").GetProperty("outputTokens").GetInt64() == 20 &&
    completionPayload.GetProperty("usage").GetProperty("totalTokens").GetInt64() == 120,
    "Actual SSE completion uses camelCase and correct usage counts");
Check(completionPayload.GetProperty("durationMs").GetInt64() == 25 &&
    completionPayload.GetProperty("reply").GetString() == "Hello",
    "Actual SSE completion includes final text and duration");
var persistedReply = await db.AIMessages.SingleAsync(x => x.ConversationId == aiConversation.Id && x.Role == "assistant");
Check(persistedReply.TotalTokenCount == 120 && persistedReply.Content == "Hello",
    "Saved reply retains the same usage and text as the stream");
Check(await chat.DeleteConversation(aiConversation.Id, CancellationToken.None) is OkObjectResult &&
    await db.AIConversations.CountAsync(x => x.UserId == player.Id) == 1 &&
    !await db.AIMessages.AnyAsync(x => x.ConversationId == aiConversation.Id),
    "Deleting the last conversation clears it while keeping one usable conversation");

var legacyGeneral = new AIConversation { Id = Guid.NewGuid(), UserId = player.Id };
db.AIConversations.Add(legacyGeneral);
await db.SaveChangesAsync();
Check((await chat.CreateConversation(new CreateConversationRequest(null), CancellationToken.None)).Result is BadRequestObjectResult,
    "Players cannot create general chat by calling the API directly");
Check((await chat.GetConversation(legacyGeneral.Id, CancellationToken.None)).Result is NotFoundResult &&
    await chat.DeleteConversation(legacyGeneral.Id, CancellationToken.None) is NotFoundResult,
    "Legacy general conversations are preserved but inaccessible to players");
var playerList = (await chat.GetConversations(CancellationToken.None)).Result as OkObjectResult;
Check(playerList?.Value is List<ConversationSummaryDto> playerChats && playerChats.All(x => x.CampaignId is not null),
    "Players only see their authorized campaign conversations");
Check((await chat.SendMessage(legacyGeneral.Id, new SendMessageRequest("Test"), CancellationToken.None)).Result is NotFoundObjectResult,
    "Players cannot send nonstreaming messages to a general conversation");
sseOutput.SetLength(0);
await chat.StreamMessage(legacyGeneral.Id, new SendMessageRequest("Test"), CancellationToken.None);
Check(chatContext.HttpContext.Response.StatusCode == 404 && !await db.AIMessages.AnyAsync(x => x.ConversationId == legacyGeneral.Id),
    "Players cannot stream messages to a general conversation");
chat.ControllerContext = Context(other);
var otherDefault = (await chat.GetOrCreateDefaultConversation(CancellationToken.None)).Result as OkObjectResult;
Check(otherDefault?.Value is ConversationSummaryDto { CampaignId: var autoCampaign } && autoCampaign == first.Id,
    "Player initialization creates a default chat in a joined campaign");
var noCampaignUser = new User { Email = $"no-campaign-{suffix}@example.test", Role = Roles.Player, PasswordHash = "unused-test-fixture" };
db.Users.Add(noCampaignUser);
await db.SaveChangesAsync();
chat.ControllerContext = Context(noCampaignUser);
Check((await chat.GetOrCreateDefaultConversation(CancellationToken.None)).Result is NoContentResult &&
    !await db.AIConversations.AnyAsync(x => x.UserId == noCampaignUser.Id),
    "Players without campaigns get an empty state without creating an unusable general chat");
chat.ControllerContext = Context(dm);
Check((await chat.CreateConversation(new CreateConversationRequest(null), CancellationToken.None)).Result is BadRequestObjectResult,
    "DMs cannot create general chats");
var dmCampaign = (await chat.CreateConversation(new CreateConversationRequest(null, first.Id), CancellationToken.None)).Result as OkObjectResult;
var dmRepeated = (await chat.CreateConversation(new CreateConversationRequest(null, first.Id), CancellationToken.None)).Result as OkObjectResult;
Check(dmCampaign?.Value is ConversationSummaryDto dmChat && dmRepeated?.Value is ConversationSummaryDto sameDmChat && dmChat.Id == sameDmChat.Id,
    "Repeated creation returns one personal DM chat for the selected campaign");
var secondDmResult = (await chat.CreateConversation(new CreateConversationRequest(null, second.Id), CancellationToken.None)).Result as OkObjectResult;
var firstDmSummary = (ConversationSummaryDto)dmCampaign!.Value!;
Check(secondDmResult?.Value is ConversationSummaryDto secondDmSummary && secondDmSummary.Id != firstDmSummary.Id && secondDmSummary.CampaignId == second.Id,
    "Different campaigns get separate personal chats");
Check(await chat.DeleteConversation(firstDmSummary.Id, CancellationToken.None) is OkObjectResult &&
    await db.AIConversations.CountAsync(x => x.UserId == dm.Id && x.CampaignId != null) == 2,
    "Clearing one campaign chat preserves both campaign conversations");
Check((await chat.GetConversation(legacyGeneral.Id, CancellationToken.None)).Result is NotFoundResult,
    "General chats are unavailable to DMs too");
chat.ControllerContext = chatContext;

// Real database, indexing and retrieval; fake provider responses avoid cost and nondeterministic network calls.
var sessions = new CampaignSessionNotesController(db, knowledge) { ControllerContext = Context(dm) };
var sessionRequest = new CreateSessionNoteRequest(1, "The silver key", "Freya found a silver key beneath the ruined tower.", new DateOnly(2026, 10, 4));
fakeEmbeddings.Fail = true;
var failedIndex = await sessions.Create(first.Id, sessionRequest, CancellationToken.None) as OkObjectResult;
Check(failedIndex?.Value is SessionNoteDto { IndexStatus: "failed", ChunkCount: 0 }, "Provider failure preserves the original session notes with retry status");
var firstNoteId = ((SessionNoteDto)failedIndex!.Value!).Id;
Check((await sessions.List(first.Id, CancellationToken.None) as OkObjectResult)?.Value is List<SessionNoteDto> saved &&
    saved.Any(n => n.Id == firstNoteId && n.Content == sessionRequest.Content), "Saved notes remain readable after indexing fails");
fakeEmbeddings.Fail = false;
Check(await sessions.RetryIndex(first.Id, firstNoteId, CancellationToken.None) is OkObjectResult { Value: SessionNoteDto { IndexStatus: "ready", ChunkCount: 1 } },
    "Retry creates a searchable index");
await sessions.RetryIndex(first.Id, firstNoteId, CancellationToken.None);
Check(await db.CampaignKnowledgeChunks.CountAsync(x => x.SessionNoteId == firstNoteId) == 1, "Repeated indexing replaces chunks without duplicates");
var unrelated = await sessions.Create(second.Id, new CreateSessionNoteRequest(1, "Secret other campaign", "A silver key opened the hidden dragon vault.", new DateOnly(2026, 10, 4)), CancellationToken.None);
var unrelatedId = ((SessionNoteDto)((OkObjectResult)unrelated).Value!).Id;
await sessions.Create(first.Id, new CreateSessionNoteRequest(2, "The orchard", "The villagers harvested apples.", new DateOnly(2026, 10, 4)), CancellationToken.None);
sessions.ControllerContext = Context(player);
Check(await sessions.Create(first.Id, sessionRequest, CancellationToken.None) is NotFoundResult, "Players cannot write notes or pay for indexing");
Check(await sessions.RetryIndex(first.Id, firstNoteId, CancellationToken.None) is NotFoundResult, "Players cannot reindex DM notes");
Check(await sessions.List(first.Id, CancellationToken.None) is OkObjectResult, "Campaign members can read shared session notes");
Check(await sessions.List(second.Id, CancellationToken.None) is OkObjectResult, "Membership permits independent access to a second campaign");
sessions.ControllerContext = Context(other);
Check(await sessions.List(second.Id, CancellationToken.None) is NotFoundResult, "Nonmembers cannot read another campaign's notes");
var retrievedContext = await knowledge.BuildContextAsync(first.Id, player.Id, "Where was the silver key found?", CancellationToken.None);
Check(retrievedContext.Sources.Count == 1 && retrievedContext.Sources[0].SessionNoteId == firstNoteId && !retrievedContext.Prompt.Contains("dragon vault"),
    "Retrieval ranks relevant passages and excludes every other campaign even for a member of both");
Check(retrievedContext.Prompt.Contains("Freya") && retrievedContext.Prompt.Contains("HitPointCurrent") && !retrievedContext.Prompt.Contains(player.Email) &&
    !retrievedContext.Prompt.Contains("PasswordHash"), "Live character facts are included without authentication or account data");
var currentCharacter = await db.Characters.FirstAsync(x => x.CampaignId == first.Id && x.UserId == player.Id);
currentCharacter.HitPointCurrent = 7;
await db.SaveChangesAsync();
var fresh = await knowledge.BuildContextAsync(first.Id, player.Id, "Where was the silver key found?", CancellationToken.None);
Check(fresh.Prompt.Contains("\"HitPointCurrent\":7"), "Character edits are available immediately without re-embedding");
var emptySearch = await knowledge.BuildContextAsync(first.Id, player.Id, "Tell me about the sea.", CancellationToken.None);
Check(emptySearch.Sources.Count == 0 && emptySearch.Prompt.Contains("No relevant campaign passages"), "Low similarity produces an explicit no-evidence result");
fakeEmbeddings.Fail = true;
var unavailable = await knowledge.BuildContextAsync(first.Id, player.Id, "silver key", CancellationToken.None);
Check(unavailable.Sources.Count == 0 && unavailable.Warning is not null && unavailable.Prompt.Contains("Freya") && !unavailable.Warning.Contains("stack"),
    "Search failures preserve current facts and return a simple warning");
fakeEmbeddings.Fail = false;
var firstNote = await db.CampaignSessionNotes.SingleAsync(n => n.Id == firstNoteId);
firstNote.EmbeddingModel = "old-model";
await db.SaveChangesAsync();
var incompatible = await knowledge.BuildContextAsync(first.Id, player.Id, "silver key", CancellationToken.None);
Check(incompatible.Sources.Count == 0 && incompatible.Warning is not null,
    "Embeddings from an incompatible model are excluded and prompt the DM to reindex");
firstNote.EmbeddingModel = fakeEmbeddings.Model;
await db.SaveChangesAsync();
var denied = false;
try { await knowledge.BuildContextAsync(second.Id, other.Id, "silver key", CancellationToken.None); }
catch (UnauthorizedAccessException) { denied = true; }
Check(denied, "Retrieval independently enforces campaign access");
chat.ControllerContext = Context(other);
Check((await chat.CreateConversation(new CreateConversationRequest(null, second.Id), CancellationToken.None)).Result is NotFoundObjectResult,
    "Nonmembers cannot create chat scoped to another campaign");
chat.ControllerContext = chatContext;
var scopedResult = (await chat.CreateConversation(new CreateConversationRequest(null, first.Id), CancellationToken.None)).Result as OkObjectResult;
var scoped = (ConversationSummaryDto)scopedResult!.Value!;
Check(scoped.CampaignId == first.Id, "Campaign association is fixed when a conversation is created");
sseOutput.SetLength(0);
await chat.StreamMessage(scoped.Id, new SendMessageRequest("Where was the silver key found?"), CancellationToken.None);
Check(fakeAi.LastContext?.Contains("silver key beneath the ruined tower") == true && !fakeAi.LastContext.Contains("dragon vault"),
    "The existing streaming chat receives the authorized retrieved context");
var scopedReply = await db.AIMessages.SingleAsync(x => x.ConversationId == scoped.Id && x.Role == "assistant");
var reloaded = (await chat.GetConversation(scoped.Id, CancellationToken.None)).Result as OkObjectResult;
Check(scopedReply.RetrievalInputTokens == 5 && reloaded?.Value is ConversationDto reload &&
    reload.CampaignId == first.Id && reload.Messages.Single(m => m.Role == "assistant").Sources?.Single().SessionNoteId == firstNoteId,
    "Source snapshots and separately billed retrieval tokens survive conversation reload");
var membership = await db.CampaignPlayer.SingleAsync(x => x.CampaignId == first.Id && x.UserId == player.Id);
db.CampaignPlayer.Remove(membership);
await db.SaveChangesAsync();
characters.ControllerContext = Context(player);
Check(await characters.GetCharacter(first.Id, editedCharacter.Id) is ForbidResult,
    "Removed player cannot read their retained character");
Check(await characters.GetCharactersByCampaign(first.Id) is ForbidResult,
    "Removed player cannot list their former campaign's characters");
var retainedHitPoints = editedCharacter.HitPointCurrent;
Check(await characters.UpdateCharacter(first.Id, editedCharacter.Id, hpUpdate with { HitPointCurrent = 0 }) is ForbidResult &&
    editedCharacter.HitPointCurrent == retainedHitPoints,
    "Removed player cannot modify their retained character");
campaigns.ControllerContext = Context(player);
Check(await campaigns.DeleteCharacter(first.Id, editedCharacter.Id) is ForbidResult &&
    await db.Characters.AnyAsync(x => x.Id == editedCharacter.Id),
    "Removed player cannot delete their retained character");
characters.ControllerContext = promotedCharacterOwner;
Check(await characters.GetCharacter(first.Id, editedCharacter.Id) is ForbidResult &&
    await characters.UpdateCharacter(first.Id, editedCharacter.Id, hpUpdate) is ForbidResult,
    "Global DM/admin roles do not restore character access after membership removal");
characters.ControllerContext = Context(dm);
Check(await characters.GetCharacter(first.Id, editedCharacter.Id) is OkObjectResult &&
    await characters.UpdateCharacter(first.Id, editedCharacter.Id, hpUpdate with { HitPointCurrent = 5 }) is NoContentResult,
    "Campaign DM retains character access after its player is removed");
Check((await chat.GetConversation(scoped.Id, CancellationToken.None)).Result is NotFoundResult, "Revoked membership prevents reading campaign conversations");
sseOutput.SetLength(0);
await chat.StreamMessage(scoped.Id, new SendMessageRequest("silver key"), CancellationToken.None);
Check(chatContext.HttpContext.Response.StatusCode == 404, "Revoked membership prevents additional campaign chat requests");
sessions.ControllerContext = Context(dm);
Check(await sessions.Delete(first.Id, unrelatedId, CancellationToken.None) is NotFoundResult, "A note cannot be deleted using another campaign's route");
Check(await sessions.Delete(first.Id, firstNoteId, CancellationToken.None) is NoContentResult &&
    !await db.CampaignKnowledgeChunks.AnyAsync(x => x.SessionNoteId == firstNoteId), "Deleting a note also deletes its derived index");
Check(JsonSerializer.Deserialize<List<KnowledgeSourceDto>>(scopedReply.SourcesJson)!.Single().SessionNoteId == firstNoteId,
    "Historical reply evidence remains after source deletion");
var fakeAudio = new FakeAudioTranscription();
var audioController = new SessionAudioController(knowledge, fakeAudio, NullLogger<SessionAudioController>.Instance)
    { ControllerContext = Context(dm) };
using var audioBytes = new MemoryStream(new byte[] { 1, 2, 3 });
SessionAudioUpload AudioFile(string name, long length = 3) => new() { Audio = new FormFile(audioBytes, 0, length, "audio", name) };
Check(await audioController.Transcribe(first.Id, AudioFile("recording.exe"), CancellationToken.None) is BadRequestObjectResult &&
    await audioController.Transcribe(first.Id, AudioFile("recording.mp3", 25000001), CancellationToken.None) is BadRequestObjectResult &&
    await audioController.Transcribe(first.Id, AudioFile("recording.mp3", 0), CancellationToken.None) is BadRequestObjectResult && fakeAudio.Calls == 0,
    "Invalid audio formats, oversized files and empty files are rejected before provider calls");
audioController.ControllerContext = Context(player);
Check(await audioController.Transcribe(first.Id, AudioFile("recording.mp3"), CancellationToken.None) is NotFoundResult && fakeAudio.Calls == 0,
    "Players cannot transcribe campaign audio");
audioController.ControllerContext = Context(other);
Check(await audioController.Transcribe(second.Id, AudioFile("recording.mp3"), CancellationToken.None) is NotFoundResult,
    "Nonowners cannot transcribe another campaign's audio");
audioController.ControllerContext = Context(dm);
fakeAudio.Fail = true;
Check(await audioController.Transcribe(first.Id, AudioFile("recording.mp3"), CancellationToken.None) is ObjectResult { StatusCode: 502, Value: string safeError } &&
    !safeError.Contains("diagnostic"), "Transcription failures return a simple error without provider diagnostics");
fakeAudio.Fail = false;
var noteCountBeforeAudio = await db.CampaignSessionNotes.CountAsync(x => x.CampaignId == first.Id);
var transcriptionResult = await audioController.Transcribe(first.Id, AudioFile("../../recording.MP3"), CancellationToken.None) as OkObjectResult;
Check(transcriptionResult?.Value is SessionTranscriptDto && fakeAudio.LastFileName == "session.mp3" &&
    await db.CampaignSessionNotes.CountAsync(x => x.CampaignId == first.Id) == noteCountBeforeAudio,
    "Audio produces a reviewable transcript without saving unapproved knowledge or using uploaded filesystem paths");
var transcript = ((SessionTranscriptDto)transcriptionResult!.Value!).Text;
var approvedAudioNote = await sessions.Create(first.Id, new CreateSessionNoteRequest(3, "Recorded session", transcript, new DateOnly(2026, 10, 4)), CancellationToken.None)
    as OkObjectResult;
var approvedId = ((SessionNoteDto)approvedAudioNote!.Value!).Id;
var audioContext = await knowledge.BuildContextAsync(first.Id, dm.Id, "silver key", CancellationToken.None);
Check(audioContext.Sources.Any(s => s.SessionNoteId == approvedId && s.Excerpt.Contains("recorded session")),
    "Approved audio transcripts flow through the existing session index and RAG retrieval");
fakeEmbeddings.BatchSizes.Clear();
var longAudioNote = await sessions.Create(first.Id, new CreateSessionNoteRequest(4, "Long recording", new string('a', 35000), new DateOnly(2026, 10, 4)), CancellationToken.None)
    as OkObjectResult;
Check(longAudioNote?.Value is SessionNoteDto { IndexStatus: "ready", ChunkCount: > 32 } longNote &&
    fakeEmbeddings.BatchSizes.Count > 1 && fakeEmbeddings.BatchSizes.All(size => size <= 32) &&
    longNote.EmbeddingInputTokens == longNote.ChunkCount * 5,
    "Long transcripts index in bounded batches and retain total embedding usage");
Check(KnowledgeText.Cosine([1, 0], [1, 0]) == 1 && KnowledgeText.Cosine([1, 0], [0, 1]) == 0 &&
    KnowledgeText.Cosine([float.NaN], [1]) == -1 && KnowledgeText.Cosine([0], [0]) == -1,
    "Cosine scoring handles matching, unrelated and invalid vectors");
var longText = string.Concat(Enumerable.Repeat("A clue 🐉 at the tower. ", 100));
var passages = KnowledgeText.Chunk(longText);
Check(passages.Count > 1 && passages.All(x => x.Length <= 1000 && !char.IsLowSurrogate(x[0]) && !char.IsHighSurrogate(x[^1])) &&
    passages[0][^150..] == passages[1][..150], "Chunking preserves overlap and Unicode boundaries");
// DM rights management and destructive cleanup use only rolled-back fixtures.
var admin = new User { Email = $"admin-{suffix}@example.test", Role = Roles.DM, IsAdmin = true, PasswordHash = "unused" };
db.Users.Add(admin);
await db.SaveChangesAsync();
var management = new DungeonMasterManagementService(db);
async Task<bool> Rejected(Func<Task> operation, int status)
{
    try { await operation(); return false; }
    catch (DmManagementException ex) { return ex.Status == status; }
}
Check(await Rejected(async () => { await management.ListAsync(dm.Id, CancellationToken.None); }, 403) &&
    await Rejected(async () => { await management.PromoteAsync(player.Id, other.Email, CancellationToken.None); }, 403),
    "Ordinary DMs and players cannot administer DM rights");
Check(await Rejected(async () => { await management.PromoteAsync(admin.Id, $"missing-admin-{suffix}@example.test", CancellationToken.None); }, 404),
    "Admin promotion rejects unregistered users");
var hashBeforePromotion = other.PasswordHash;
var promoted = await management.PromoteAsync(admin.Id, " " + other.Email.ToUpperInvariant() + " ", CancellationToken.None);
Check(promoted.Role == Roles.DM && !promoted.IsAdmin && other.PasswordHash == hashBeforePromotion,
    "Admin promotes an existing user with case-insensitive lookup without granting admin or changing credentials");
var versionAfterPromotion = other.TokenVersion;
await management.PromoteAsync(admin.Id, other.Email, CancellationToken.None);
Check(other.TokenVersion == versionAfterPromotion, "Repeated DM promotion is idempotent");
Check((await management.ListAsync(admin.Id, CancellationToken.None)).Any(u => u.Id == other.Id), "Admin can read the current DM roster");
Check(await Rejected(async () => { await management.PreviewAsync(admin.Id, admin.Id, CancellationToken.None); }, 409) &&
    await Rejected(() => management.RemoveAsync(admin.Id, admin.Id, 0, CancellationToken.None), 409),
    "The seeded administrator cannot demote itself");
var dmOwned = new Campaign { OwnerId = other.Id, Name = "DM cleanup fixture", Description = "Temporary" };
db.Campaigns.Add(dmOwned);
await db.SaveChangesAsync();
db.CampaignPlayer.Add(new CampaignPlayer { CampaignId = dmOwned.Id, UserId = player.Id });
db.Characters.Add(new Character { CampaignId = dmOwned.Id, UserId = player.Id, Name = "Cleanup character", Class = "Fighter", Race = "Human", Background = "Soldier", Alignment = "Neutral" });
db.CharacterClassOptions.Add(new CharacterClassOption { CampaignId = dmOwned.Id, UserId = other.Id, Name = "Cleanup class", NormalizedName = "CLEANUP CLASS" });
db.CharacterRaceOptions.Add(new CharacterRaceOption { CampaignId = dmOwned.Id, UserId = other.Id, Name = "Cleanup race", NormalizedName = "CLEANUP RACE" });
db.CharacterBackgroundOptions.Add(new CharacterBackgroundOption { CampaignId = dmOwned.Id, UserId = other.Id, Name = "Cleanup background", NormalizedName = "CLEANUP BACKGROUND" });
var cleanupGeneral = new AIConversation { Id = Guid.NewGuid(), UserId = other.Id };
var memberChat = new AIConversation { Id = Guid.NewGuid(), UserId = player.Id, CampaignId = dmOwned.Id };
db.AIConversations.AddRange(cleanupGeneral, memberChat);
db.AIMessages.Add(new AIMessage { ConversationId = memberChat.Id, Role = "assistant", Content = "Campaign secret" });
await db.SaveChangesAsync();
Check(await db.Entry(other).Collection(u => u.CharacterClassOptions).Query()
    .AnyAsync(c => c.CampaignId == dmOwned.Id && c.Name == "Cleanup class" && c.UserId == other.Id),
    "User class-option navigation reads records owned through UserId");
var navigationClass = new CharacterClassOption { CampaignId = dmOwned.Id, Name = "Navigation class", NormalizedName = "NAVIGATION CLASS" };
other.CharacterClassOptions.Add(navigationClass);
await db.SaveChangesAsync();
Check(navigationClass.UserId == other.Id && await db.CharacterClassOptions.AsNoTracking()
    .AnyAsync(c => c.Id == navigationClass.Id && c.UserId == other.Id),
    "Adding a class through the user collection persists the correct owner");
sessions.ControllerContext = Context(other);
var cleanupNote = (SessionNoteDto)((OkObjectResult)await sessions.Create(dmOwned.Id,
    new CreateSessionNoteRequest(1, "Cleanup session", "A silver key", new DateOnly(2026, 10, 4)), CancellationToken.None)).Value!;
var preservedCharacter = await db.Characters.SingleAsync(c => c.UserId == other.Id && c.CampaignId == first.Id);
var preservedMemberships = await db.CampaignPlayer.CountAsync(p => p.UserId == other.Id && p.CampaignId == first.Id);
var previewRemoval = await management.PreviewAsync(admin.Id, other.Id, CancellationToken.None);
Check(previewRemoval.CampaignCount == 1 && previewRemoval.CharacterCount == 1 && previewRemoval.SessionNoteCount == 1 &&
    previewRemoval.CampaignConversationCount == 1 && previewRemoval.GeneralConversationCount == 1 && previewRemoval.PreservedCharacterCount == 1,
    "Cleanup preview counts deleted campaign data and preserved player characters");
Check(await Rejected(() => management.RemoveAsync(admin.Id, other.Id, 0, CancellationToken.None), 409) &&
    await db.Campaigns.AnyAsync(c => c.Id == dmOwned.Id), "A stale cleanup preview rejects removal without deleting data");

var testKey = new string('x', 64);
var jwtService = new JwtService(Options.Create(new JwtOptions { Issuer = "test", Audience = "test", SigningKeys = [new JwtSigningKey { Kid = "test", Key = testKey }] }));
ClaimsPrincipal TokenPrincipal(User account) => new JwtSecurityTokenHandler().ValidateToken(jwtService.GenerateToken(account),
    new TokenValidationParameters { ValidIssuer = "test", ValidAudience = "test", IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(testKey)), ClockSkew = TimeSpan.Zero }, out _);
var tokenValidator = new CurrentTokenValidator(db);
var oldDmPrincipal = TokenPrincipal(other);
Check(await tokenValidator.IsCurrentAsync(oldDmPrincipal, CancellationToken.None) && TokenPrincipal(admin).IsInRole(Roles.Admin) &&
    TokenPrincipal(admin).IsInRole(Roles.DM), "Real JWTs carry current token versions and admins retain both DM and Admin claims");
db.RefreshTokens.Add(new RefreshToken { UserId = other.Id, Token = Guid.NewGuid().ToString(), ExpiresAt = DateTime.UtcNow.AddDays(1) });
await db.SaveChangesAsync();
await management.RemoveAsync(admin.Id, other.Id, 1, CancellationToken.None);
Check(other.Role == Roles.Player && !other.IsAdmin && other.PasswordHash == hashBeforePromotion &&
    !await db.Campaigns.AnyAsync(c => c.OwnerId == other.Id), "DM removal preserves the account as a player and deletes owned campaigns");
Check(!await db.Characters.AnyAsync(c => c.CampaignId == dmOwned.Id) && !await db.CampaignPlayer.AnyAsync(c => c.CampaignId == dmOwned.Id) &&
    !await db.CharacterClassOptions.AnyAsync(c => c.CampaignId == dmOwned.Id) && !await db.CharacterRaceOptions.AnyAsync(c => c.CampaignId == dmOwned.Id) &&
    !await db.CharacterBackgroundOptions.AnyAsync(c => c.CampaignId == dmOwned.Id), "DM cleanup cascades to all owned-campaign characters, members and custom options");
Check(!await db.CampaignSessionNotes.AnyAsync(n => n.CampaignId == dmOwned.Id) && !await db.CampaignKnowledgeChunks.AnyAsync(c => c.SessionNoteId == cleanupNote.Id) &&
    !await db.AIConversations.AnyAsync(c => c.CampaignId == dmOwned.Id || c.Id == cleanupGeneral.Id) && !await db.AIMessages.AnyAsync(m => m.ConversationId == memberChat.Id),
    "DM cleanup removes session knowledge and all users' owned-campaign chats plus the former DM's general chat");
Check(await db.Characters.AnyAsync(c => c.Id == preservedCharacter.Id) &&
    await db.CampaignPlayer.CountAsync(p => p.UserId == other.Id && p.CampaignId == first.Id) == preservedMemberships &&
    await db.AIConversations.AnyAsync(c => c.UserId == other.Id && c.CampaignId == first.Id),
    "The former DM retains player characters, memberships and personal player chats in other campaigns");
Check(!await tokenValidator.IsCurrentAsync(oldDmPrincipal, CancellationToken.None) && !await db.RefreshTokens.AnyAsync(t => t.UserId == other.Id),
    "Demotion immediately invalidates previous access and refresh tokens");
Check(await tokenValidator.IsCurrentAsync(TokenPrincipal(other), CancellationToken.None), "A fresh player login token remains valid after demotion");
var lateCreation = new CampaignsController(db) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = oldDmPrincipal } } };
Check((await lateCreation.CreateCampaign(new CreateCampaignDto("Late campaign", "Should not be created", other.Id))).Result is ForbidResult,
    "A request carrying the former DM claim cannot recreate an owned campaign after cleanup");
// Map CRUD and retrieval use the real PostgreSQL schema, with fake embeddings and rollback fixtures.
var mapsController = new CampaignMapsController(db, knowledge) { ControllerContext = Context(dm) };
using var mapBitmap = new SKBitmap(1200, 600);
var mapRandom = new Random(42);
mapBitmap.Pixels = Enumerable.Range(0, 1200 * 600)
    .Select(_ => new SKColor((byte)mapRandom.Next(256), (byte)mapRandom.Next(256), (byte)mapRandom.Next(256))).ToArray();
using var testMapImage = SKImage.FromBitmap(mapBitmap);
using var pngData = testMapImage.Encode(SKEncodedImageFormat.Png, 100);
var png = pngData.ToArray();
foreach (var format in new[] { SKEncodedImageFormat.Png, SKEncodedImageFormat.Jpeg, SKEncodedImageFormat.Webp })
{
    using var encoded = testMapImage.Encode(format, 85);
    var previewBytes = MapThumbnail.Create(encoded.ToArray());
    using var previewBitmap = previewBytes is null ? null : SKBitmap.Decode(previewBytes);
    Check(previewBytes is { Length: > 0 and <= MapThumbnail.MaxBytes } && previewBitmap is not null &&
        previewBitmap.Width <= MapThumbnail.MaxDimension && previewBitmap.Height <= MapThumbnail.MaxDimension &&
        previewBitmap.Width == previewBitmap.Height * 2,
        $"{format} previews preserve aspect ratio and stay within pixel and byte limits");
}
Check(MapThumbnail.Create(Encoding.UTF8.GetBytes("invalid image")) is null,
    "Invalid images cannot produce previews");
using (var jpeg = testMapImage.Encode(SKEncodedImageFormat.Jpeg, 85))
{
    var bytes = jpeg.ToArray();
    var orientation = Convert.FromHexString("FFE1002245786966000049492A0008000000010012010300010000000600000000000000");
    var oriented = bytes.Take(2).Concat(orientation).Concat(bytes.Skip(2)).ToArray();
    using var preview = SKBitmap.Decode(MapThumbnail.Create(oriented));
    Check(preview is not null && preview.Height == preview.Width * 2,
        "JPEG preview dimensions honor EXIF rotation like the original browser image");
}
IFormFile MapFile() => new FormFile(new MemoryStream(png), 0, png.Length, "image", "map.png");
SaveMapRequest MapRequest(string description, bool image = false) => new() {
    Title = "Tower map", Description = description,
    LocationsJson = JsonSerializer.Serialize(new[] { new MapLocation("Ruined tower", description, 25, 75) }),
    Image = image ? MapFile() : null };
Check(await mapsController.Create(first.Id, new SaveMapRequest { Title = "Invalid", LocationsJson = "[]", Image = MapFile() }, CancellationToken.None) is BadRequestObjectResult,
    "Maps require at least one named location");
Check(await mapsController.Create(first.Id, new SaveMapRequest { Title = "Invalid", LocationsJson = "null", Image = MapFile() }, CancellationToken.None) is BadRequestObjectResult,
    "Null map locations are rejected");
Check(CampaignMapsController.DetectImageType(Encoding.UTF8.GetBytes("<svg onload='alert(1)'>")) is null,
    "Unsupported active image formats are rejected");
fakeEmbeddings.Fail = true;
var mapCreated = (MapDto)((OkObjectResult)await mapsController.Create(first.Id, MapRequest("A silver key is kept here.", true), CancellationToken.None)).Value!;
sqlCommands.Clear();
var thumbnailResult = (FileContentResult)await mapsController.Thumbnail(first.Id, mapCreated.Id, CancellationToken.None);
Check(thumbnailResult.ContentType == "image/jpeg" && thumbnailResult.FileContents.Length <= MapThumbnail.MaxBytes &&
    thumbnailResult.FileContents.Length < png.Length / 10 && !sqlCommands.Any(sql => sql.Contains("\"Image\"")),
    "Stored thumbnail requests return bounded JPEGs without selecting the original image");
await db.CampaignMaps.Where(m => m.Id == mapCreated.Id).ExecuteUpdateAsync(s => s.SetProperty(m => m.Thumbnail, (byte[]?)null));
var legacyThumbnail = (FileContentResult)await mapsController.Thumbnail(first.Id, mapCreated.Id, CancellationToken.None);
Check(legacyThumbnail.FileContents.Length <= MapThumbnail.MaxBytes &&
    await db.CampaignMaps.AsNoTracking().Where(m => m.Id == mapCreated.Id).Select(m => m.Thumbnail != null).SingleAsync(),
    "Legacy maps generate and persist their preview on first access");
sqlCommands.Clear();
await mapsController.Thumbnail(first.Id, mapCreated.Id, CancellationToken.None);
Check(!sqlCommands.Any(sql => sql.Contains("\"Image\"")), "Subsequent legacy preview requests reuse the stored thumbnail");
var duplicateMapRequest = MapRequest("Duplicate", true);
duplicateMapRequest.Title = "  tower MAP  ";
Check(await mapsController.Create(first.Id, duplicateMapRequest, CancellationToken.None) is ConflictObjectResult,
    "Duplicate map names ignore casing and surrounding spaces");
var distinctRequest = MapRequest("Separate map", true); distinctRequest.Title = "Forest map";
var distinctMap = (MapDto)((OkObjectResult)await mapsController.Create(first.Id, distinctRequest, CancellationToken.None)).Value!;
Check(await mapsController.Update(first.Id, distinctMap.Id, MapRequest("Conflicting rename"), CancellationToken.None) is ConflictObjectResult,
    "Renaming a map cannot reuse another map's name");
Check(await mapsController.Update(first.Id, mapCreated.Id, MapRequest("A silver key is kept here."), CancellationToken.None) is OkObjectResult,
    "Saving a map with its own name is allowed");
var otherCampaignMap = (MapDto)((OkObjectResult)await mapsController.Create(second.Id, MapRequest("Different campaign", true), CancellationToken.None)).Value!;
Check(otherCampaignMap.Title == mapCreated.Title, "Different campaigns may use the same map name");
await mapsController.Delete(second.Id, otherCampaignMap.Id, CancellationToken.None);
await mapsController.Delete(first.Id, distinctMap.Id, CancellationToken.None);
Check(mapCreated.IndexStatus == "pending" && await db.CampaignMaps.AnyAsync(x => x.Id == mapCreated.Id && x.Image.Length > 0),
    "Map saves queue durable indexing while preserving the image and locations");
fakeEmbeddings.Fail = false;
await mapsController.Retry(first.Id, mapCreated.Id, CancellationToken.None);
await mapsController.Retry(first.Id, mapCreated.Id, CancellationToken.None);
Check(await db.MapKnowledgeChunks.CountAsync(x => x.MapId == mapCreated.Id) == 0 &&
    await db.CampaignMaps.AnyAsync(x => x.Id == mapCreated.Id && x.IndexStatus == "pending"), "Map retries queue indexing without calling embeddings in the request");
var mapContext = await knowledge.BuildContextAsync(first.Id, dm.Id, "silver key", CancellationToken.None);
Check(!mapContext.Sources.Any(x => x.MapId == mapCreated.Id) && mapContext.Prompt.Contains("Ruined tower"),
    "Pending maps supply current location facts while semantic retrieval waits for indexing");
Check(!(await knowledge.BuildContextAsync(second.Id, dm.Id, "silver key", CancellationToken.None)).Sources.Any(x => x.MapId == mapCreated.Id),
    "Map retrieval isolates campaigns");
mapsController.ControllerContext = Context(noCampaignUser);
Check(await mapsController.List(first.Id, CancellationToken.None) is NotFoundResult &&
    await mapsController.Image(first.Id, mapCreated.Id, CancellationToken.None) is NotFoundResult,
    "Nonmembers cannot list maps or download their images");
Check(await mapsController.Thumbnail(first.Id, mapCreated.Id, CancellationToken.None) is NotFoundResult,
    "Nonmembers cannot download map thumbnails");
mapsController.ControllerContext = Context(player);
// Membership was revoked earlier in these checks. Add it back for map read checks.
if (!await db.CampaignPlayer.AnyAsync(x => x.CampaignId == first.Id && x.UserId == player.Id)) {
    db.CampaignPlayer.Add(new CampaignPlayer { CampaignId = first.Id, UserId = player.Id }); await db.SaveChangesAsync();
}
Check(await mapsController.List(first.Id, CancellationToken.None) is OkObjectResult &&
    await mapsController.Image(first.Id, mapCreated.Id, CancellationToken.None) is FileContentResult,
    "Campaign members can view maps and protected images");
Check(await mapsController.Thumbnail(first.Id, mapCreated.Id, CancellationToken.None) is FileContentResult &&
    await mapsController.Thumbnail(second.Id, mapCreated.Id, CancellationToken.None) is NotFoundResult,
    "Thumbnail access requires campaign membership and the matching campaign route");
Check(await mapsController.Update(first.Id, mapCreated.Id, MapRequest("Changed"), CancellationToken.None) is NotFoundResult &&
    await mapsController.Delete(first.Id, mapCreated.Id, CancellationToken.None) is NotFoundResult &&
    await mapsController.Retry(first.Id, mapCreated.Id, CancellationToken.None) is NotFoundResult,
    "Members cannot edit, delete or reindex maps");
mapsController.ControllerContext = Context(dm);
Check(await mapsController.Update(second.Id, mapCreated.Id, MapRequest("Wrong campaign"), CancellationToken.None) is NotFoundResult,
    "Map CRUD checks map and campaign together");
fakeEmbeddings.Fail = true;
await mapsController.Update(first.Id, mapCreated.Id, MapRequest("Apples grow here now."), CancellationToken.None);
fakeEmbeddings.Fail = false;
Check(!(await knowledge.BuildContextAsync(first.Id, dm.Id, "silver key", CancellationToken.None)).Sources.Any(x => x.MapId == mapCreated.Id),
    "Failed indexing after edits excludes obsolete map passages");
await mapsController.Retry(first.Id, mapCreated.Id, CancellationToken.None);
var updatedMapContext = await knowledge.BuildContextAsync(first.Id, dm.Id, "apple", CancellationToken.None);
Check(updatedMapContext.Prompt.Contains("Apples grow") && !updatedMapContext.Sources.Any(x => x.MapId == mapCreated.Id) &&
    (await mapsController.Image(first.Id, mapCreated.Id, CancellationToken.None) as FileContentResult)!.FileContents.SequenceEqual(png),
    "Editing updates retrieved text while retaining the image when no replacement is uploaded");
var previousThumbnail = ((FileContentResult)await mapsController.Thumbnail(first.Id, mapCreated.Id, CancellationToken.None)).FileContents;
using var replacementBitmap = new SKBitmap(100, 50);
replacementBitmap.Erase(SKColors.Blue);
using var replacementImage = SKImage.FromBitmap(replacementBitmap);
using var replacementData = replacementImage.Encode(SKEncodedImageFormat.Png, 100);
var replacementBytes = replacementData.ToArray();
var replacementRequest = MapRequest("Apples grow here now.");
replacementRequest.Image = new FormFile(new MemoryStream(replacementBytes), 0, replacementBytes.Length, "image", "replacement.png");
Check(await mapsController.Update(first.Id, mapCreated.Id, replacementRequest, CancellationToken.None) is OkObjectResult &&
    !((FileContentResult)await mapsController.Thumbnail(first.Id, mapCreated.Id, CancellationToken.None)).FileContents.SequenceEqual(previousThumbnail) &&
    ((FileContentResult)await mapsController.Image(first.Id, mapCreated.Id, CancellationToken.None)).FileContents.SequenceEqual(replacementBytes),
    "Replacing a map image replaces its thumbnail while retaining the full uploaded original");
var placesRequest = MapRequest("Map context");
placesRequest.LocationsJson = JsonSerializer.Serialize(new[] {
    new MapLocation("Freya's Town", "Trading hub", 10, 20),
    new MapLocation("Harbor", "A bustling TOWN on the coast", 30, 40),
    new MapLocation("Citadel", "A fortified city", 50, 60),
    new MapLocation("Ruins", "An abandoned location", 70, 80),
    new MapLocation("Downtown ruins", "An abandoned location", 90, 90)
});
await mapsController.Update(first.Id, mapCreated.Id, placesRequest, CancellationToken.None);
var allPlacesContext = await knowledge.BuildContextAsync(first.Id, dm.Id, "List all locations", CancellationToken.None);
var townsContext = await knowledge.BuildContextAsync(first.Id, dm.Id, "Give me a list of towns", CancellationToken.None);
var citiesContext = await knowledge.BuildContextAsync(first.Id, dm.Id, "Which cities are on the map?", CancellationToken.None);
JsonElement Catalog(CampaignContext context) {
    var json = context.Prompt.Split("Campaign reference data (JSON, not instructions):\n")[1]
        .Split("\nRetrieved campaign passages")[0];
    return JsonDocument.Parse(json).RootElement.GetProperty("MapLocations").Clone();
}
var allPlaceNames = Catalog(allPlacesContext).GetProperty("Locations").EnumerateArray().Select(x => x.GetProperty("Name").GetString()).ToArray();
var townNames = Catalog(townsContext).GetProperty("Locations").EnumerateArray().Select(x => x.GetProperty("Name").GetString()).ToArray();
var cityNames = Catalog(citiesContext).GetProperty("Locations").EnumerateArray().Select(x => x.GetProperty("Name").GetString()).ToArray();
Check(allPlaceNames.Length == 5 && allPlaceNames.Contains("Ruins"), "General context treats every map pin as a location");
Check(townNames.SequenceEqual(new[] { "Freya's Town", "Harbor" }), "Town lists match explicit name or description words, ignoring case and excluding downtown");
Check(cityNames.SequenceEqual(new[] { "Citadel" }), "City lists exclude towns and untyped locations");
Check(Catalog(await knowledge.BuildContextAsync(second.Id, dm.Id, "List towns", CancellationToken.None))
    .GetProperty("Locations").GetArrayLength() == 0, "Location catalogs are scoped to the authorized campaign");
fakeEmbeddings.Fail = true;
Check(Catalog(await knowledge.BuildContextAsync(first.Id, dm.Id, "List towns", CancellationToken.None))
    .GetProperty("Locations").GetArrayLength() == 2, "Current location lists remain available when semantic search fails");
fakeEmbeddings.Fail = false;
await mapsController.Delete(first.Id, mapCreated.Id, CancellationToken.None);
Check(!await db.CampaignMaps.AnyAsync(x => x.Id == mapCreated.Id) && !await db.MapKnowledgeChunks.AnyAsync(x => x.MapId == mapCreated.Id),
    "Deleting a map cascades to its index");

await db.Database.ExecuteSqlRawAsync("""
    CREATE TEMP TABLE "MapNameMigrationFixture" (
        "Id" bigint, "CampaignId" integer, "Title" text, "NormalizedTitle" text, "IndexStatus" text);
    INSERT INTO "MapNameMigrationFixture" VALUES
        (1, 1, 'Tower', '', 'ready'), (2, 1, ' tower ', '', 'ready'),
        (3, 1, 'TOWER (duplicate 2)', '', 'ready'), (4, 2, 'Tower', '', 'ready');
    """);
var nameMigrationSql = new DnDCampaignManager.Api.Migrations.UniqueCampaignMapNames().UpOperations
    .OfType<Microsoft.EntityFrameworkCore.Migrations.Operations.SqlOperation>().Single().Sql;
await db.Database.ExecuteSqlRawAsync(nameMigrationSql.Replace("\"CampaignMaps\"", "\"MapNameMigrationFixture\""));
var migrationValid = await db.Database.SqlQueryRaw<int>("""
    SELECT count(*)::integer AS "Value" FROM "MapNameMigrationFixture"
    WHERE ("Id" = 1 AND "Title" = 'Tower' AND "IndexStatus" = 'ready')
       OR ("Id" = 2 AND "Title" = 'tower (duplicate 2-1)' AND "IndexStatus" = 'pending')
       OR ("Id" = 3 AND "Title" = 'TOWER (duplicate 2)' AND "IndexStatus" = 'ready')
       OR ("Id" = 4 AND "Title" = 'Tower' AND "IndexStatus" = 'ready')
    """).SingleAsync();
Check(migrationValid == 4, "Name migration preserves all maps, resolves suffix collisions and flags renamed maps for indexing");
await transaction.RollbackAsync();
Console.WriteLine("All membership checks passed; all test data rolled back.");

sealed class FakeAIService : IAIService
{
    public string? LastContext { get; private set; }
    public string Model => "test";
    private static AICompletionResult Completion => new("Hello", "test", "test-response",
        new AIUsage(100, 20, 120, 0.001m), 25);
    public Task<AICompletionResult> GetChatResponseAsync(IReadOnlyCollection<AIMessage> history,
        CancellationToken cancellationToken = default, string? campaignContext = null, CampaignToolScope? toolScope = null)
    { LastContext = campaignContext; return Task.FromResult(Completion); }
    public async IAsyncEnumerable<AIStreamEvent> StreamChatResponseAsync(IReadOnlyCollection<AIMessage> history,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default, string? campaignContext = null, CampaignToolScope? toolScope = null)
    {
        LastContext = campaignContext;
        await Task.Yield();
        yield return new AIStreamEvent("token", "Hello");
        yield return new AIStreamEvent("completed", Completion: Completion);
    }
}

sealed class FakeEmbeddings : IEmbeddingService
{
    public List<int> BatchSizes { get; } = [];
    public string Model => "test-embedding";
    public int Dimensions => 3;
    public bool Fail { get; set; }
    public Task<EmbeddingBatch> EmbedAsync(IReadOnlyList<string> inputs, CancellationToken cancellationToken)
    {
        BatchSizes.Add(inputs.Count);
        if (Fail) throw new InvalidOperationException("Provider stack diagnostics must never reach the interface.");
        var vectors = inputs.Select(text => text.Contains("silver key", StringComparison.OrdinalIgnoreCase) ? new float[] { 1, 0, 0 }
            : text.Contains("apple", StringComparison.OrdinalIgnoreCase) ? new float[] { 0, 1, 0 } : new float[] { 0, 0, 1 }).ToArray();
        return Task.FromResult(new EmbeddingBatch(vectors, 5 * inputs.Count));
    }
}

sealed class FakeAudioTranscription : IAudioTranscriptionService
{
    public string Model => "test-transcription";
    public bool Fail { get; set; }
    public int Calls { get; private set; }
    public string? LastFileName { get; private set; }
    public Task<string> TranscribeAsync(Stream audio, string fileName, CancellationToken ct)
    {
        Calls++;
        LastFileName = fileName;
        if (Fail) throw new InvalidOperationException("Provider diagnostic stack must not be displayed.");
        return Task.FromResult("In this recorded session, Freya found a silver key beside a waterfall.");
    }
}

sealed class SqlRecorder(List<string> commands) : DbCommandInterceptor
{
    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
        CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
        commands.Add(command.CommandText);
        return ValueTask.FromResult(result);
    }
}

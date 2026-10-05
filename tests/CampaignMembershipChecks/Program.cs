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

var apiDirectory = Path.GetFullPath(args[0]);
var configuration = new ConfigurationBuilder()
    .SetBasePath(apiDirectory)
    .AddJsonFile("appsettings.json")
    .AddJsonFile("appsettings.Development.json", optional: true)
    .AddUserSecrets(typeof(DnDxDbContext).Assembly, optional: true)
    .AddEnvironmentVariables()
    .Build();
var options = new DbContextOptionsBuilder<DnDxDbContext>()
    .UseNpgsql(configuration.GetConnectionString("DefaultConnection")).Options;
await using var db = new DnDxDbContext(options);
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
Check(await characters.CreateCharacter(first.Id, request) is BadRequestObjectResult, "One character per user per campaign is enforced");
Check(await characters.CreateCharacter(second.Id, request) is OkObjectResult, "Same user can create a different character in another campaign");
characters.ControllerContext = Context(other);
Check(await characters.CreateCharacter(first.Id, request) is OkObjectResult, "One campaign supports multiple players' characters");
await transaction.RollbackAsync();
Console.WriteLine("All membership checks passed; all test data rolled back.");

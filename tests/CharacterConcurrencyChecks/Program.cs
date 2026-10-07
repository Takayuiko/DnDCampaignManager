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

var config = new ConfigurationBuilder().SetBasePath(Path.GetFullPath(args[0])).AddJsonFile("appsettings.json")
    .AddJsonFile("appsettings.Development.json", optional: true).AddUserSecrets(typeof(DnDxDbContext).Assembly, optional: true).AddEnvironmentVariables().Build();
DnDxDbContext Database() => new(new DbContextOptionsBuilder<DnDxDbContext>().UseNpgsql(config.GetConnectionString("DefaultConnection")).Options);
void Check(bool value, string message) { if (!value) throw new Exception(message); Console.WriteLine("PASS: " + message); }
await using var fixture = Database();
await fixture.Database.MigrateAsync();
var dm = new User { Email = $"character-dm-{Guid.NewGuid():N}@example.test", Role = Roles.DM, PasswordHash = "fixture" };
var player = new User { Email = $"character-player-{Guid.NewGuid():N}@example.test", Role = Roles.Player, PasswordHash = "fixture" };
fixture.Users.AddRange(dm, player); await fixture.SaveChangesAsync();
var campaign = new Campaign { OwnerId = dm.Id, Name = "Concurrency fixture", Description = "Temporary" };
fixture.Campaigns.Add(campaign); await fixture.SaveChangesAsync();
try {
    fixture.CampaignPlayer.Add(new CampaignPlayer { CampaignId = campaign.Id, UserId = player.Id });
    var character = new Character { CampaignId = campaign.Id, UserId = player.Id, Name = "Freya", Class = "Fighter", Race = "Human", Background = "Soldier", Alignment = "Neutral", HitPointCurrent = 10, HitPointMax = 10 };
    fixture.Characters.Add(character); await fixture.SaveChangesAsync();
    CharactersController Controller(DnDxDbContext db, User user) => new(db) { ControllerContext = new() { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()), new Claim(ClaimTypes.Role, user.Role)], "test")) } } };
    await using var reader = Database();
    var response = (CharacterResponseDto)((OkObjectResult)await Controller(reader, player).GetCharacter(campaign.Id, character.Id)).Value!;
    var request = JsonSerializer.Deserialize<UpdateCharacterDto>(JsonSerializer.Serialize(response))!;
    await using var first = Database();
    await using var second = Database();
    var attempts = await Task.WhenAll(Controller(first, player).UpdateCharacter(campaign.Id, character.Id, request with { HitPointCurrent = 7 }),
        Controller(second, dm).UpdateCharacter(campaign.Id, character.Id, request with { HitPointCurrent = 3 }));
    Check(attempts.Count(r => r is OkObjectResult) == 1 && attempts.Count(r => r is ConflictObjectResult) == 1, "Two simultaneous saves with the same version have exactly one winner");
    var latest = await fixture.Characters.AsNoTracking().SingleAsync(c => c.Id == character.Id);
    Check(latest.HitPointCurrent is 7 or 3 && latest.Version != response.Version, "Winning save changes both character data and version");
    await using var staleDb = Database();
    Check(await Controller(staleDb, player).UpdateCharacter(campaign.Id, character.Id, request with { Name = "Overwrite", Attacks = [new() { Name = "Stale attack" }] }) is ConflictObjectResult &&
        !await fixture.CharacterAttacks.AnyAsync(a => a.CharacterId == character.Id), "Stale saves change neither character fields nor child collections");
    Check(await Controller(staleDb, player).UpdateCharacter(campaign.Id, character.Id, request with { Version = null }) is BadRequestObjectResult, "Missing version cannot bypass conflict detection");
    await using var reloadedDb = Database();
    var reloaded = (CharacterResponseDto)((OkObjectResult)await Controller(reloadedDb, player).GetCharacter(campaign.Id, character.Id)).Value!;
    var fresh = JsonSerializer.Deserialize<UpdateCharacterDto>(JsonSerializer.Serialize(reloaded))!;
    Check(await Controller(reloadedDb, player).UpdateCharacter(campaign.Id, character.Id, fresh with { Name = "Reviewed" }) is OkObjectResult, "Reloading the current version permits an intentional new save");
    var final = await fixture.Characters.AsNoTracking().SingleAsync(c => c.Id == character.Id);
    Check(final.HitPointCurrent == latest.HitPointCurrent && final.Name == "Reviewed", "Reloaded edits preserve the winner's unrelated changes");
} finally {
    await fixture.Campaigns.Where(c => c.Id == campaign.Id).ExecuteDeleteAsync();
    await fixture.Users.Where(u => u.Id == dm.Id || u.Id == player.Id).ExecuteDeleteAsync();
}
Console.WriteLine("Character concurrency checks passed; fixtures removed.");

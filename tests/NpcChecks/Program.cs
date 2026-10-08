using System.Security.Claims;
using System.Text.Json;
using DnDCampaignManager.Api.Controllers;
using DnDCampaignManager.Api.DTOs;
using DnDCampaignManager.Api.Models;
using DnDCampingManager.Api.Data;
using DnDCampingManager.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

var config = new ConfigurationBuilder().SetBasePath(Path.GetFullPath(args[0])).AddJsonFile("appsettings.json")
    .AddJsonFile("appsettings.Development.json", optional: true).AddUserSecrets(typeof(DnDxDbContext).Assembly, optional: true).AddEnvironmentVariables().Build();
DnDxDbContext Database() => new(new DbContextOptionsBuilder<DnDxDbContext>().UseNpgsql(config.GetConnectionString("DefaultConnection")).Options);
void Check(bool value, string message) { if (!value) throw new Exception(message); Console.WriteLine("PASS: " + message); }
CampaignNpcsController Controller(DnDxDbContext db, User user) => new(db) { ControllerContext = new() { HttpContext = new DefaultHttpContext {
    User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()), new Claim(ClaimTypes.Role, user.Role)], "test")) } } };
await using var fixture = Database();
await fixture.Database.MigrateAsync();
var dm = new User { Email = $"npc-dm-{Guid.NewGuid():N}@example.test", Role = Roles.DM, PasswordHash = "fixture" };
var player = new User { Email = $"npc-player-{Guid.NewGuid():N}@example.test", Role = Roles.Player, PasswordHash = "fixture" };
var outsider = new User { Email = $"npc-outsider-{Guid.NewGuid():N}@example.test", Role = Roles.DM, PasswordHash = "fixture" };
fixture.Users.AddRange(dm, player, outsider); await fixture.SaveChangesAsync();
var campaign = new Campaign { OwnerId = dm.Id, Name = "NPC fixture", Description = "Temporary" };
var other = new Campaign { OwnerId = outsider.Id, Name = "Other fixture", Description = "Temporary" };
fixture.Campaigns.AddRange(campaign, other); await fixture.SaveChangesAsync();
try {
    fixture.CampaignPlayer.Add(new CampaignPlayer { CampaignId = campaign.Id, UserId = player.Id });
    var map = new CampaignMap { CampaignId = campaign.Id, Title = "Town", NormalizedTitle = "TOWN", LocationsJson = JsonSerializer.Serialize(new[] { new MapLocation("Inn", "", 50, 50) }) };
    var foreignMap = new CampaignMap { CampaignId = other.Id, Title = "Other", NormalizedTitle = "OTHER", LocationsJson = map.LocationsJson };
    fixture.CampaignMaps.AddRange(map, foreignMap); await fixture.SaveChangesAsync();
    foreach (var method in new[] { "Create", "Update", "Delete" })
        Check(typeof(CampaignNpcsController).GetMethod(method)!.GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>().Any(x => x.Roles == "DM"), method + " requires the DM role");
    SaveNpcRequest Request(long mapId, Guid? version = null) => new() { Name = " Freya ", Description = " A guide ", MapId = mapId, LocationName = "Inn", IsPartyMember = true, Version = version };
    await using var write = Database();
    Check(await Controller(write, player).Create(campaign.Id, Request(map.Id), default) is NotFoundResult, "Players cannot create NPCs");
    Check(await Controller(write, outsider).Create(campaign.Id, Request(map.Id), default) is NotFoundResult, "Another DM cannot manage this campaign");
    Check(await Controller(write, dm).Create(campaign.Id, Request(foreignMap.Id), default) is BadRequestObjectResult, "Cross-campaign map references are rejected");
    var created = (NpcDto)((CreatedResult)await Controller(write, dm).Create(campaign.Id, Request(map.Id), default)).Value!;
    Check(created.Name == "Freya" && created.IsPartyMember && !created.HasImage, "DM creates an NPC with trimmed fields, party membership and no image");
    await using var read = Database();
    var list = (NpcCatalogDto)((OkObjectResult)await Controller(read, player).List(campaign.Id, default)).Value!;
    Check(!list.CanManage && list.Npcs.Count == 1, "Campaign members can browse NPCs without management access");
    Check(await Controller(read, outsider).Get(campaign.Id, created.Id, default) is NotFoundResult, "Outsiders cannot read NPCs");
    Check(await Controller(read, outsider).Image(campaign.Id, created.Id, default) is NotFoundResult, "Outsiders cannot read portraits");
    await using var update = Database();
    var current = (NpcDto)((OkObjectResult)await Controller(update, dm).Update(campaign.Id, created.Id, Request(map.Id, created.Version), default)).Value!;
    var invalid = Request(map.Id, current.Version); invalid.LocationName = "Missing";
    Check(await Controller(update, dm).Update(campaign.Id, created.Id, invalid, default) is BadRequestObjectResult, "Missing map locations are rejected");
    using var bitmap = new SkiaSharp.SKBitmap(8, 8);
    using var image = SkiaSharp.SKImage.FromBitmap(bitmap);
    using var encoded = image.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
    using var stream = new MemoryStream(encoded.ToArray());
    var withImage = Request(map.Id, current.Version);
    withImage.Image = new FormFile(stream, 0, stream.Length, "Image", "portrait.png");
    withImage.IsPartyMember = false;
    current = (NpcDto)((OkObjectResult)await Controller(update, dm).Update(campaign.Id, created.Id, withImage, default)).Value!;
    Check(current.HasImage && !current.IsPartyMember, "DM can upload a portrait and remove NPC from the party");
    var portrait = (FileContentResult)await Controller(read, player).Image(campaign.Id, created.Id, default);
    Check(portrait.ContentType == "image/png" && portrait.FileContents.SequenceEqual(encoded.ToArray()), "Campaign members can read the exact original portrait");
    var thumbnail = (FileContentResult)await Controller(read, player).Thumbnail(campaign.Id, created.Id, default);
    Check(thumbnail.ContentType == "image/jpeg" && thumbnail.FileContents.Length > 0, "Campaign members can read a separate portrait thumbnail");
    var withoutImage = Request(map.Id, current.Version); withoutImage.RemoveImage = true;
    current = (NpcDto)((OkObjectResult)await Controller(update, dm).Update(campaign.Id, created.Id, withoutImage, default)).Value!;
    Check(!current.HasImage, "DM can remove the portrait");
    await using var stale = Database();
    Check(await Controller(stale, dm).Update(campaign.Id, created.Id, Request(map.Id, created.Version), default) is ConflictObjectResult, "Stale edits cannot overwrite newer saves");
    Check(await Controller(stale, player).Delete(campaign.Id, created.Id, default) is NotFoundResult, "Players cannot delete NPCs");
    await fixture.CampaignMaps.Where(x => x.Id == map.Id).ExecuteDeleteAsync();
    var preserved = await fixture.CampaignNpcs.AsNoTracking().SingleAsync(x => x.Id == created.Id);
    Check(preserved.MapId == null && preserved.LocationName == "Inn", "Deleting a map preserves NPCs and their previous location name");
    await using var delete = Database();
    Check(await Controller(delete, dm).Delete(campaign.Id, created.Id, default) is NoContentResult, "DM can delete NPCs");
    Check(!await fixture.CampaignNpcs.AnyAsync(x => x.Id == created.Id), "Deleted NPC is removed from storage");
} finally {
    await fixture.Campaigns.Where(x => x.Id == campaign.Id || x.Id == other.Id).ExecuteDeleteAsync();
    await fixture.Users.Where(x => x.Id == dm.Id || x.Id == player.Id || x.Id == outsider.Id).ExecuteDeleteAsync();
}
Console.WriteLine("NPC checks passed; fixtures removed.");

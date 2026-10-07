using DnDCampaignManager.Api.DTOs;
using DnDCampaignManager.Api.Models;
using DnDCampaignManager.Api.Services.AI;
using DnDCampingManager.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;

namespace DnDCampaignManager.Api.Controllers;

[ApiController, Authorize, Route("api/campaigns/{campaignId:int}/maps")]
public sealed class CampaignMapsController(DnDxDbContext db, CampaignKnowledgeService access,
    MapKnowledgeService index) : ControllerBase
{
    private int UserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private static MapDto Dto(CampaignMap map) => new(map.Id, map.CampaignId, map.Title,
        map.Description, JsonSerializer.Deserialize<List<MapLocation>>(map.LocationsJson)!, map.IndexStatus);

    [HttpGet]
    public async Task<IActionResult> List(int campaignId, CancellationToken ct)
    {
        if (!await access.CanAccessAsync(campaignId, UserId, ct)) return NotFound();
        var maps = await db.CampaignMaps.AsNoTracking().Where(x => x.CampaignId == campaignId)
            .OrderBy(x => x.Title).Select(x => new { x.Id, x.CampaignId, x.Title, x.Description, x.LocationsJson, x.IndexStatus }).ToListAsync(ct);
        return Ok(maps.Select(x => new MapDto(x.Id, x.CampaignId, x.Title, x.Description,
            JsonSerializer.Deserialize<List<MapLocation>>(x.LocationsJson)!, x.IndexStatus)));
    }

    [HttpGet("{mapId:long}/image")]
    public async Task<IActionResult> Image(int campaignId, long mapId, CancellationToken ct)
    {
        if (!await access.CanAccessAsync(campaignId, UserId, ct)) return NotFound();
        var map = await db.CampaignMaps.AsNoTracking().SingleOrDefaultAsync(x => x.Id == mapId && x.CampaignId == campaignId, ct);
        if (map is null) return NotFound();
        Response.Headers.CacheControl = "no-store";
        Response.Headers.XContentTypeOptions = "nosniff";
        return File(map.Image, map.ImageContentType);
    }

    [HttpPost, Authorize(Roles = "DM"), EnableRateLimiting("ai")]
    [RequestSizeLimit(51000000), RequestFormLimits(MultipartBodyLengthLimit = 51000000)]
    public Task<IActionResult> Create(int campaignId, [FromForm] SaveMapRequest request, CancellationToken ct) => Save(campaignId, null, request, ct);

    [HttpPut("{mapId:long}"), Authorize(Roles = "DM"), EnableRateLimiting("ai")]
    [RequestSizeLimit(51000000), RequestFormLimits(MultipartBodyLengthLimit = 51000000)]
    public Task<IActionResult> Update(int campaignId, long mapId, [FromForm] SaveMapRequest request, CancellationToken ct) => Save(campaignId, mapId, request, ct);

    private async Task<IActionResult> Save(int campaignId, long? mapId, SaveMapRequest request, CancellationToken ct)
    {
        if (!await access.CanManageAsync(campaignId, UserId, ct)) return NotFound();
        List<MapLocation>? locations;
        try { locations = JsonSerializer.Deserialize<List<MapLocation>>(request.LocationsJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }); }
        catch (JsonException) { return BadRequest(new { error = "Invalid locations." }); }
        if (string.IsNullOrWhiteSpace(request.Title) || locations is null || locations.Count is < 1 or > 50 ||
            locations.Any(x => x is null || string.IsNullOrWhiteSpace(x.Name) || x.Name.Length > 120 ||
                x.Description is null || x.Description.Length > 4000 || !double.IsFinite(x.X) || !double.IsFinite(x.Y) ||
                x.X is < 0 or > 100 || x.Y is < 0 or > 100))
            return BadRequest(new { error = "Enter a title and 1–50 named locations with valid descriptions and pin coordinates." });
        byte[]? image = null;
        string? mime = null;
        if (request.Image is not null)
        {
            if (request.Image.Length is <= 0 or > 50000000) return BadRequest(new { error = "Choose an image up to 50 MB." });
            using var buffer = new MemoryStream();
            await request.Image.CopyToAsync(buffer, ct);
            image = buffer.ToArray();
            mime = DetectImageType(image);
            if (mime is null) return BadRequest(new { error = "Choose a PNG, JPEG or WebP image." });
        }
        if (mapId is null && image is null) return BadRequest(new { error = "An image is required." });
        await using var transaction = db.Database.CurrentTransaction is null ? await db.Database.BeginTransactionAsync(ct) : null;
        // Serialize name checks within a campaign, as the item catalog does.
        var campaign = await db.Campaigns.FromSqlInterpolated(
            $"SELECT * FROM \"Campaigns\" WHERE \"Id\" = {campaignId} FOR UPDATE").SingleOrDefaultAsync(ct);
        if (campaign is null || campaign.OwnerId != UserId) return NotFound();
        var map = mapId is null ? new CampaignMap { CampaignId = campaignId } : await LockedMap(campaignId, mapId.Value, ct);
        if (map is null) return NotFound();
        var normalizedTitle = request.Title.Trim().ToUpperInvariant();
        if (await db.CampaignMaps.AnyAsync(x => x.CampaignId == campaignId && x.NormalizedTitle == normalizedTitle &&
            (!mapId.HasValue || x.Id != mapId.Value), ct))
            return Conflict(new { error = "A map with that name already exists in this campaign." });
        if (mapId is null) db.CampaignMaps.Add(map);
        map.Title = request.Title.Trim(); map.Description = (request.Description ?? "").Trim();
        map.NormalizedTitle = normalizedTitle;
        map.LocationsJson = JsonSerializer.Serialize(locations.Select(x => x with { Name = x.Name.Trim(), Description = x.Description.Trim() }));
        if (image is not null) { map.Image = image; map.ImageContentType = mime!; }
        await index.IndexAsync(map, ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
        return Ok(Dto(map));
    }

    public static string? DetectImageType(byte[] image)
    {
        if (image.Length >= 24 && image.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })) return "image/png";
        if (image.Length >= 4 && image[0] == 255 && image[1] == 216 && image[2] == 255) return "image/jpeg";
        if (image.Length >= 16 && image.AsSpan(0, 4).SequenceEqual("RIFF"u8) && image.AsSpan(8, 4).SequenceEqual("WEBP"u8)) return "image/webp";
        return null;
    }

    private Task<CampaignMap?> LockedMap(int campaignId, long mapId, CancellationToken ct) => db.CampaignMaps
        .FromSqlInterpolated($"SELECT * FROM \"CampaignMaps\" WHERE \"Id\" = {mapId} AND \"CampaignId\" = {campaignId} FOR UPDATE").SingleOrDefaultAsync(ct);

    [HttpPost("{mapId:long}/index"), Authorize(Roles = "DM"), EnableRateLimiting("ai")]
    public async Task<IActionResult> Retry(int campaignId, long mapId, CancellationToken ct)
    {
        if (!await access.CanManageAsync(campaignId, UserId, ct)) return NotFound();
        await using var transaction = db.Database.CurrentTransaction is null ? await db.Database.BeginTransactionAsync(ct) : null;
        var map = await LockedMap(campaignId, mapId, ct);
        if (map is null) return NotFound();
        await index.IndexAsync(map, ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
        return Ok(Dto(map));
    }

    [HttpDelete("{mapId:long}"), Authorize(Roles = "DM")]
    public async Task<IActionResult> Delete(int campaignId, long mapId, CancellationToken ct)
    {
        if (!await access.CanManageAsync(campaignId, UserId, ct)) return NotFound();
        await using var transaction = db.Database.CurrentTransaction is null ? await db.Database.BeginTransactionAsync(ct) : null;
        var map = await LockedMap(campaignId, mapId, ct);
        if (map is null) return NotFound();
        db.CampaignMaps.Remove(map);
        await db.SaveChangesAsync(ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
        return NoContent();
    }
}

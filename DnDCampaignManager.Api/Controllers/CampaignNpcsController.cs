using System.Security.Claims;
using System.Text.Json;
using DnDCampaignManager.Api.DTOs;
using DnDCampaignManager.Api.Models;
using DnDCampaignManager.Api.Services;
using DnDCampingManager.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DnDCampaignManager.Api.Controllers;

[ApiController, Authorize, Route("api/campaigns/{campaignId:int}/npcs")]
public sealed class CampaignNpcsController(DnDxDbContext db) : ControllerBase
{
    private int UserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private static NpcDto Dto(CampaignNpc npc) => new(npc.Id, npc.CampaignId, npc.Name,
        npc.Description, npc.IsPartyMember, npc.MapId, npc.LocationName, npc.Image != null, npc.Version);

    [HttpGet]
    public async Task<IActionResult> List(int campaignId, CancellationToken ct)
    {
        if (!await CampaignAuthorization.CanAccessAsync(db, campaignId, UserId, ct)) return NotFound();
        var npcs = await db.CampaignNpcs.AsNoTracking().Where(x => x.CampaignId == campaignId)
            .OrderBy(x => x.Name).Select(x => new NpcDto(x.Id, x.CampaignId, x.Name, x.Description,
                x.IsPartyMember, x.MapId, x.LocationName, x.Image != null, x.Version)).ToListAsync(ct);
        return Ok(new NpcCatalogDto(User.IsInRole("DM") &&
            await CampaignAuthorization.CanManageAsync(db, campaignId, UserId, ct), npcs));
    }

    [HttpGet("{npcId:long}/image")]
    public async Task<IActionResult> Image(int campaignId, long npcId, CancellationToken ct)
    {
        if (!await CampaignAuthorization.CanAccessAsync(db, campaignId, UserId, ct)) return NotFound();
        var portrait = await db.CampaignNpcs.AsNoTracking().Where(x => x.CampaignId == campaignId && x.Id == npcId)
            .Select(x => new { Image = x.OriginalImage ?? x.Image, x.OriginalImageContentType }).SingleOrDefaultAsync(ct);
        if (portrait?.Image is null) return NotFound();
        Response.Headers.CacheControl = "no-store";
        Response.Headers.XContentTypeOptions = "nosniff";
        return File(portrait.Image, portrait.OriginalImageContentType ?? "image/jpeg");
    }

    [HttpGet("{npcId:long}/thumbnail")]
    public async Task<IActionResult> Thumbnail(int campaignId, long npcId, CancellationToken ct)
    {
        if (!await CampaignAuthorization.CanAccessAsync(db, campaignId, UserId, ct)) return NotFound();
        var bytes = await db.CampaignNpcs.AsNoTracking().Where(x => x.CampaignId == campaignId && x.Id == npcId)
            .Select(x => x.Image).SingleOrDefaultAsync(ct);
        if (bytes is null) return NotFound();
        Response.Headers.CacheControl = "no-store";
        Response.Headers.XContentTypeOptions = "nosniff";
        return File(bytes, "image/jpeg");
    }

    [HttpPost, Authorize(Roles = "DM")]
    [RequestSizeLimit(11000000), RequestFormLimits(MultipartBodyLengthLimit = 11000000)]
    public Task<IActionResult> Create(int campaignId, [FromForm] SaveNpcRequest request, CancellationToken ct) => Save(campaignId, null, request, ct);

    [HttpPut("{npcId:long}"), Authorize(Roles = "DM")]
    [RequestSizeLimit(11000000), RequestFormLimits(MultipartBodyLengthLimit = 11000000)]
    public Task<IActionResult> Update(int campaignId, long npcId, [FromForm] SaveNpcRequest request, CancellationToken ct) => Save(campaignId, npcId, request, ct);

    private async Task<IActionResult> Save(int campaignId, long? npcId, SaveNpcRequest request, CancellationToken ct)
    {
        if (!await CampaignAuthorization.CanManageAsync(db, campaignId, UserId, ct)) return NotFound();
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Description) ||
            string.IsNullOrWhiteSpace(request.LocationName)) return BadRequest(new { error = "Name, description and map location are required." });
        byte[]? portrait = null;
        byte[]? original = null;
        string? contentType = null;
        if (request.Image is not null)
        {
            if (request.RemoveImage) return BadRequest(new { error = "Choose either a replacement image or remove image." });
            if (request.Image.Length is <= 0 or > 10000000) return BadRequest(new { error = "Choose an image up to 10 MB." });
            using var buffer = new MemoryStream();
            await request.Image.CopyToAsync(buffer, ct);
            var source = buffer.ToArray();
            contentType = CampaignMapsController.DetectImageType(source);
            if (contentType is null || (portrait = MapThumbnail.Create(source)) is null)
                return BadRequest(new { error = "Choose a valid PNG, JPEG or WebP image." });
            original = source;
        }
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        // Match map writes: lock campaign, then map, so location validation cannot race a map edit/delete.
        var campaign = await db.Campaigns.FromSqlInterpolated($"SELECT * FROM \"Campaigns\" WHERE \"Id\" = {campaignId} FOR UPDATE").SingleOrDefaultAsync(ct);
        if (campaign is null || campaign.OwnerId != UserId) return NotFound();
        var map = await db.CampaignMaps.FromSqlInterpolated($"SELECT * FROM \"CampaignMaps\" WHERE \"Id\" = {request.MapId} AND \"CampaignId\" = {campaignId} FOR UPDATE").SingleOrDefaultAsync(ct);
        if (map is null || !JsonSerializer.Deserialize<List<MapLocation>>(map.LocationsJson)!
            .Any(x => x.Name == request.LocationName.Trim())) return BadRequest(new { error = "Choose an existing location on a map in this campaign." });
        var npc = npcId is null ? new CampaignNpc { CampaignId = campaignId } :
            await db.CampaignNpcs.SingleOrDefaultAsync(x => x.Id == npcId && x.CampaignId == campaignId, ct);
        if (npc is null) return NotFound();
        if (npcId.HasValue && request.Version != npc.Version) return Conflict(new { error = "This NPC changed. Reload before editing again." });
        npc.Name = request.Name.Trim(); npc.Description = request.Description.Trim();
        npc.MapId = map.Id; npc.LocationName = request.LocationName.Trim(); npc.IsPartyMember = request.IsPartyMember;
        if (portrait is not null) { npc.Image = portrait; npc.OriginalImage = original; npc.OriginalImageContentType = contentType; }
        else if (request.RemoveImage) { npc.Image = null; npc.OriginalImage = null; npc.OriginalImageContentType = null; }
        npc.Version = Guid.NewGuid();
        if (npcId is null) db.CampaignNpcs.Add(npc);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Conflict(new { error = "This NPC changed. Reload before editing again." }); }
        await transaction.CommitAsync(ct);
        return npcId is null ? Created($"/api/campaigns/{campaignId}/npcs/{npc.Id}", Dto(npc)) : Ok(Dto(npc));
    }

    [HttpGet("{npcId:long}")]
    public async Task<IActionResult> Get(int campaignId, long npcId, CancellationToken ct)
    {
        if (!await CampaignAuthorization.CanAccessAsync(db, campaignId, UserId, ct)) return NotFound();
        var npc = await db.CampaignNpcs.AsNoTracking().SingleOrDefaultAsync(x => x.Id == npcId && x.CampaignId == campaignId, ct);
        return npc is null ? NotFound() : Ok(Dto(npc));
    }

    [HttpDelete("{npcId:long}"), Authorize(Roles = "DM")]
    public async Task<IActionResult> Delete(int campaignId, long npcId, CancellationToken ct)
    {
        if (!await CampaignAuthorization.CanManageAsync(db, campaignId, UserId, ct)) return NotFound();
        var deleted = await db.CampaignNpcs.Where(x => x.Id == npcId && x.CampaignId == campaignId).ExecuteDeleteAsync(ct);
        return deleted == 0 ? NotFound() : NoContent();
    }
}

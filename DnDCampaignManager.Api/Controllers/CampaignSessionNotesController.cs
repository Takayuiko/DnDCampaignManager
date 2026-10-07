using DnDCampaignManager.Api.DTOs.AI_DTO;
using DnDCampaignManager.Api.Models.AI;
using DnDCampaignManager.Api.Services.AI;
using DnDCampingManager.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace DnDCampaignManager.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/campaigns/{campaignId:int}/session-notes")]
public sealed class CampaignSessionNotesController(DnDxDbContext db, CampaignKnowledgeService knowledge) : ControllerBase
{
    private int UserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> List(int campaignId, CancellationToken ct)
    {
        if (!await knowledge.CanAccessAsync(campaignId, UserId, ct)) return NotFound();
        return Ok(await db.CampaignSessionNotes.AsNoTracking().Where(x => x.CampaignId == campaignId)
            .OrderByDescending(x => x.SessionNumber).ThenByDescending(x => x.Id).Select(x => new SessionNoteDto(
                x.Id, x.CampaignId, x.SessionNumber, x.Title, x.Content, x.PlayedOn, x.CreatedAtUtc,
                x.IndexStatus, x.EmbeddingModel, x.EmbeddingDimensions, x.EmbeddingInputTokens, x.Chunks.Count)).ToListAsync(ct));
    }

    [HttpPost]
    [Authorize(Roles = "DM")]
    [EnableRateLimiting("ai")]
    public async Task<IActionResult> Create(int campaignId, CreateSessionNoteRequest request, CancellationToken ct)
    {
        if (!await knowledge.CanManageAsync(campaignId, UserId, ct)) return NotFound();
        if (request.PlayedOn == default || string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Content))
            return BadRequest(new { error = "Enter a session date, title and notes." });
        var note = new CampaignSessionNote
        {
            CampaignId = campaignId, SessionNumber = request.SessionNumber, Title = request.Title.Trim(),
            Content = request.Content.Trim(), PlayedOn = request.PlayedOn
        };
        db.CampaignSessionNotes.Add(note);
        // Save the source first. A provider failure must not lose the DM's notes.
        await db.SaveChangesAsync(ct);
        await knowledge.IndexAsync(note.Id, campaignId, UserId, ct);
        return Ok(await GetDto(note.Id, ct));
    }

    [HttpPost("{noteId:long}/index")]
    [Authorize(Roles = "DM")]
    [EnableRateLimiting("ai")]
    public async Task<IActionResult> RetryIndex(int campaignId, long noteId, CancellationToken ct)
    {
        if (!await knowledge.CanManageAsync(campaignId, UserId, ct) ||
            !await db.CampaignSessionNotes.AnyAsync(x => x.Id == noteId && x.CampaignId == campaignId, ct)) return NotFound();
        await knowledge.IndexAsync(noteId, campaignId, UserId, ct);
        return Ok(await GetDto(noteId, ct));
    }

    [HttpDelete("{noteId:long}")]
    [Authorize(Roles = "DM")]
    public async Task<IActionResult> Delete(int campaignId, long noteId, CancellationToken ct)
    {
        if (!await knowledge.CanManageAsync(campaignId, UserId, ct)) return NotFound();
        var note = await db.CampaignSessionNotes.SingleOrDefaultAsync(x => x.Id == noteId && x.CampaignId == campaignId, ct);
        if (note is null) return NotFound();
        db.CampaignSessionNotes.Remove(note);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    private Task<SessionNoteDto> GetDto(long noteId, CancellationToken ct) => db.CampaignSessionNotes.AsNoTracking()
        .Where(x => x.Id == noteId).Select(x => new SessionNoteDto(x.Id, x.CampaignId, x.SessionNumber,
            x.Title, x.Content, x.PlayedOn, x.CreatedAtUtc, x.IndexStatus, x.EmbeddingModel,
            x.EmbeddingDimensions, x.EmbeddingInputTokens, x.Chunks.Count)).SingleAsync(ct);
}

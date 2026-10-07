using DnDCampaignManager.Api.Services.AI;
using DnDCampaignManager.Api.DTOs.AI_DTO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;

namespace DnDCampaignManager.Api.Controllers;

[ApiController]
[Authorize(Roles = "DM")]
[EnableRateLimiting("ai")]
[Route("api/campaigns/{campaignId:int}/session-notes/audio")]
public sealed class SessionAudioController(CampaignKnowledgeService knowledge, IAudioTranscriptionService transcription,
    ILogger<SessionAudioController> logger) : ControllerBase
{
    public const long MaxAudioBytes = 25_000_000;
    private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
        { ".mp3", ".m4a", ".wav", ".webm", ".mpeg", ".mpga" };

    [HttpPost("transcribe")]
    [RequestSizeLimit(26_000_000)]
    [RequestFormLimits(MultipartBodyLengthLimit = 26_000_000)]
    public async Task<IActionResult> Transcribe(int campaignId, [FromForm] SessionAudioUpload request, CancellationToken ct)
    {
        var audio = request.Audio;
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        // Check both role and campaign ownership before any paid provider call.
        if (!User.IsInRole("DM") || !await knowledge.CanManageAsync(campaignId, userId, ct)) return NotFound();
        if (!transcription.IsAvailable)
            return StatusCode(StatusCodes.Status503ServiceUnavailable, AIServiceRegistration.UnavailableMessage);
        if (audio is null || audio.Length == 0) return BadRequest("Select a nonempty audio recording.");
        if (audio.Length > MaxAudioBytes) return BadRequest("Audio must be 25 MB or smaller. Compress it or upload separate parts.");
        var extension = Path.GetExtension(audio.FileName);
        if (!Extensions.Contains(extension)) return BadRequest("Supported audio formats: MP3, M4A, WAV, WEBM, MPEG and MPGA.");
        try
        {
            await using var stream = audio.OpenReadStream();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromMinutes(10));
            // Use a neutral filename; never use an uploaded name as a server filesystem path.
            var text = (await transcription.TranscribeAsync(stream, "session" + extension.ToLowerInvariant(), timeout.Token)).Trim();
            if (string.IsNullOrWhiteSpace(text)) return UnprocessableEntity("No speech was transcribed. Try a clearer recording.");
            if (text.Length > 240000) return UnprocessableEntity("The transcript is too long. Split the recording into smaller parts.");
            return Ok(new SessionTranscriptDto(text, transcription.Model));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            logger.LogError(ex, "Session audio transcription failed for campaign {CampaignId}.", campaignId);
            return StatusCode(StatusCodes.Status502BadGateway, "Unable to transcribe this recording. Please try again or use another audio file.");
        }
    }
}


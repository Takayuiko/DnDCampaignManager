namespace DnDCampaignManager.Api.Services.AI;

public interface IAudioTranscriptionService
{
    string Model { get; }
    Task<string> TranscribeAsync(Stream audio, string fileName, CancellationToken ct);
}

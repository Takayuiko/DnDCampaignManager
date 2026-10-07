namespace DnDCampaignManager.Api.Services.AI;

public interface IAudioTranscriptionService
{
    bool IsAvailable => true;
    string Model { get; }
    Task<string> TranscribeAsync(Stream audio, string fileName, CancellationToken ct);
}

using OpenAI.Audio;

namespace DnDCampaignManager.Api.Services.AI;

public sealed class OpenAIAudioTranscriptionService(AudioClient client, IConfiguration configuration) : IAudioTranscriptionService
{
    public string Model => configuration["OpenAI:TranscriptionModel"] ?? "gpt-transcribe";

    public async Task<string> TranscribeAsync(Stream audio, string fileName, CancellationToken ct)
    {
        using var deadline = new AIRequestLimits(configuration).Deadline(ct, new AIRequestLimits(configuration).TranscriptionTimeoutSeconds);
        ct = deadline.Token;
        AudioTranscription result = await client.TranscribeAudioAsync(audio, fileName, new AudioTranscriptionOptions(), ct);
        return result.Text;
    }
}

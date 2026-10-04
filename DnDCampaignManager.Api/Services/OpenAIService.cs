using DnDCampaignManager.Api.DTOs.AI_DTO;
using OpenAI.Responses;

namespace DnDCampaignManager.Api.Services;

public sealed class OpenAIService : IAIService
{
    private readonly ResponsesClient _client;
    private readonly IConfiguration _configuration;

    public OpenAIService(
        ResponsesClient client,
        IConfiguration configuration)
    {
        _client = client;
        _configuration = configuration;
    }

    public string Model =>
        _configuration["OpenAI:Model"] ?? "gpt-5.2";

    public async Task<string> GetChatResponseAsync(
        string userMessage,
        IReadOnlyCollection<ChatMessage> history,
        CancellationToken cancellationToken = default)
    {
        var options = new CreateResponseOptions
        {
            Model = Model
        };

        options.InputItems.Add(
            ResponseItem.CreateDeveloperMessageItem(
                "You are a helpful D&D campaign assistant. " +
                "Help with campaigns, lore, NPCs, characters and D&D rules. " +
                "Be clear when something is uncertain or depends on campaign-specific information."));

        foreach (var message in history)
        {
            if (string.Equals(message.Role, "assistant", StringComparison.OrdinalIgnoreCase))
            {
                options.InputItems.Add(
                    ResponseItem.CreateAssistantMessageItem(message.Content));
            }
            else
            {
                options.InputItems.Add(
                    ResponseItem.CreateUserMessageItem(message.Content));
            }
        }

        options.InputItems.Add(
            ResponseItem.CreateUserMessageItem(userMessage));

        var response = await _client.CreateResponseAsync(
            options,
            cancellationToken);

        return response.GetOutputText();
    }
}

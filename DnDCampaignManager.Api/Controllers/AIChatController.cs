using DnDCampaignManager.Api.DTOs.AI_DTO;
using DnDCampaignManager.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace DnDCampaignManager.Api.Controllers;

[ApiController]
[Route("api/ai")]
public class AIChatController : ControllerBase
{
    private readonly IAIService _aiService;
    private readonly IConfiguration _configuration;

    public AIChatController(
        IAIService aiService,
        IConfiguration configuration)
    {
        _aiService = aiService;
        _configuration = configuration;
    }

    [HttpGet("status")]
    public IActionResult Status()
    {
        var configured =
            !string.IsNullOrWhiteSpace(_configuration["OpenAI:ApiKey"]);

        return Ok(new
        {
            configured,
            provider = "OpenAI",
            model = _aiService.Model
        });
    }

    [HttpPost("chat")]
    public async Task<ActionResult<ChatResponse>> Chat(
        [FromBody] ChatRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            return BadRequest(new
            {
                error = "Message cannot be empty."
            });
        }

        // Keep Phase 1 intentionally simple. Conversation history is supplied
        // by the client for now; persistence will be introduced in Phase 2.
        var history = request.History
            .Where(m =>
                (m.Role.Equals("user", StringComparison.OrdinalIgnoreCase) ||
                 m.Role.Equals("assistant", StringComparison.OrdinalIgnoreCase)) &&
                !string.IsNullOrWhiteSpace(m.Content))
            .TakeLast(20)
            .ToList();

        var reply = await _aiService.GetChatResponseAsync(
            request.Message,
            history,
            cancellationToken);

        return Ok(new ChatResponse(reply, _aiService.Model));
    }
}

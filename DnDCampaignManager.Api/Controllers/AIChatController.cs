using DnDCampaignManager.Api.Services;
using DnDCampaignManager.Api.DTOs.AI_DTO;
using DnDCampaignManager.Api.Models.AI;
using DnDCampaignManager.Api.Services.AI;
using DnDCampingManager.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;
using System.Text;

namespace DnDCampaignManager.Api.Controllers;

[ApiController]
[Route("api/ai")]
[EnableRateLimiting("ai")]
public class AIChatController : ControllerBase
{
    private const int MaxMessageLength = 8000;
    private const string ConversationBusy = "This conversation has an active request. Wait for it to finish before sending or clearing messages.";
    private static readonly JsonSerializerOptions SseJsonOptions = new(JsonSerializerDefaults.Web);

    private readonly DnDxDbContext _db;
    private readonly IAIService _aiService;
    private readonly AIRequestLimits _limits;
    private readonly ILogger<AIChatController> _logger;
    private readonly CampaignKnowledgeService _knowledge;

    public AIChatController(
        DnDxDbContext db,
        IAIService aiService,
        IConfiguration configuration,
        ILogger<AIChatController> logger,
        CampaignKnowledgeService knowledge)
    {
        _db = db;
        _aiService = aiService;
        _limits = new(configuration);
        _logger = logger;
        _knowledge = knowledge;
    }

    [HttpGet("status")]
    public IActionResult Status()
    {
        var configured = _aiService.IsAvailable;

        return Ok(new
        {
            configured,
            provider = "OpenAI",
            model = _aiService.Model
        });
    }

    [HttpGet("conversations")]
    public async Task<ActionResult<IReadOnlyList<ConversationSummaryDto>>> GetConversations(
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();

        var conversations = await AccessibleConversations(userId)
            .OrderByDescending(x => x.UpdatedAtUtc)
            .Select(x => new ConversationSummaryDto(
                x.Id,
                x.Title,
                x.CreatedAtUtc,
                x.UpdatedAtUtc,
                x.Messages.Count,
                x.CampaignId))
            .ToListAsync(cancellationToken);

        return Ok(conversations);
    }

    [HttpPost("conversations")]
    public async Task<ActionResult<ConversationSummaryDto>> CreateConversation(
        [FromBody] CreateConversationRequest? request,
        CancellationToken cancellationToken)
    {
        if (request?.CampaignId is not int campaignId)
            return BadRequest(new { error = "Select a campaign you belong to." });
        return await GetOrCreateCampaignConversation(campaignId, cancellationToken);
    }

    [HttpPost("conversations/default")]
    public async Task<ActionResult<ConversationSummaryDto>> GetOrCreateDefaultConversation(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var campaignId = await _db.Campaigns.AccessibleTo(userId)
            .OrderBy(c => c.Id).Select(c => (int?)c.Id).FirstOrDefaultAsync(cancellationToken);
        return campaignId is int id ? await GetOrCreateCampaignConversation(id, cancellationToken) : NoContent();
    }

    private async Task<ActionResult<ConversationSummaryDto>> GetOrCreateCampaignConversation(int campaignId, CancellationToken ct)
    {
        var userId = GetUserId();
        await using var transaction = _db.Database.CurrentTransaction is null
            ? await _db.Database.BeginTransactionAsync(ct) : null;
        var user = await _db.Users.FromSqlInterpolated(
            $"SELECT * FROM \"Users\" WHERE \"Id\" = {userId} FOR UPDATE")
            .SingleOrDefaultAsync(ct);
        if (user is null) return Unauthorized();
        if (!await _knowledge.CanAccessAsync(campaignId, userId, ct))
            return NotFound(new { error = "Campaign not found." });
        var conversation = await AccessibleConversations(userId).SingleOrDefaultAsync(x => x.CampaignId == campaignId, ct);
        if (conversation is null)
        {
            conversation = new AIConversation { Id = Guid.NewGuid(), UserId = userId, CampaignId = campaignId };
            _db.AIConversations.Add(conversation);
            await _db.SaveChangesAsync(ct);
        }
        var count = await _db.AIMessages.CountAsync(x => x.ConversationId == conversation.Id, ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
        return Ok(new ConversationSummaryDto(conversation.Id, conversation.Title,
            conversation.CreatedAtUtc, conversation.UpdatedAtUtc, count, conversation.CampaignId));
    }

    [HttpGet("conversations/{conversationId:guid}")]
    public async Task<ActionResult<ConversationDto>> GetConversation(
        Guid conversationId,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();

        var conversation = await LoadConversationAsync(conversationId, userId, cancellationToken);
        if (conversation is null) return NotFound();
        var messages = await _db.AIMessages.AsNoTracking().Where(m => m.ConversationId == conversationId)
            .OrderByDescending(m => m.CreatedAtUtc).ThenByDescending(m => m.Id).Take(200).ToListAsync(cancellationToken);
        return Ok(new ConversationDto(conversation.Id, conversation.Title, conversation.CreatedAtUtc,
            conversation.UpdatedAtUtc, messages.OrderBy(m => m.CreatedAtUtc).ThenBy(m => m.Id).Select(m =>
                new ConversationMessageDto(m.Id, m.Role, m.Content, m.CreatedAtUtc, m.Model,
                    m.InputTokenCount, m.OutputTokenCount, m.TotalTokenCount, m.EstimatedCostUsd,
                    m.DurationMs, m.Status, JsonSerializer.Deserialize<List<KnowledgeSourceDto>>(m.SourcesJson),
                    m.RetrievalInputTokens, m.RetrievalWarning)).ToList(), conversation.CampaignId));
    }

    [HttpDelete("conversations/{conversationId:guid}")]
    public async Task<IActionResult> DeleteConversation(
        Guid conversationId,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var conversation = await LoadConversationAsync(conversationId, userId, cancellationToken);
        if (conversation is null) return NotFound();
        await using var operation = await ConversationOperation.TryAcquireAsync(_db, conversationId, cancellationToken);
        if (operation is null) return Conflict(new { error = ConversationBusy });
        await _db.Entry(conversation).ReloadAsync(cancellationToken);
        await using var transaction = _db.Database.CurrentTransaction is null
            ? await _db.Database.BeginTransactionAsync(cancellationToken) : null;

        await _db.AIMessages.Where(x => x.ConversationId == conversationId).ExecuteDeleteAsync(cancellationToken);
        conversation.Title = "New AI Conversation";
        conversation.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        return Ok(new ConversationSummaryDto(conversation.Id, conversation.Title,
            conversation.CreatedAtUtc, conversation.UpdatedAtUtc, 0, conversation.CampaignId));
    }

    [HttpPost("conversations/{conversationId:guid}/messages")]
    public async Task<ActionResult<ChatResponse>> SendMessage(
        Guid conversationId,
        [FromBody] SendMessageRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryValidateMessage(request.Message, out var error))
            return BadRequest(new { error });

        var userId = GetUserId();
        var conversation = await LoadConversationAsync(conversationId, userId, cancellationToken);
        if (conversation is null)
            return NotFound(new { error = "Conversation not found." });

        if (!_aiService.IsAvailable)
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = AIServiceRegistration.UnavailableMessage });

        await using var operation = await ConversationOperation.TryAcquireAsync(_db, conversationId, cancellationToken);
        if (operation is null) return Conflict(new { error = ConversationBusy });
        await _db.Entry(conversation).ReloadAsync(cancellationToken);

        var userMessage = CreateUserMessage(conversationId, request.Message);
        _db.AIMessages.Add(userMessage);
        UpdateConversation(conversation, request.Message);
        await _db.SaveChangesAsync(cancellationToken);

        using var deadline = _limits.Deadline(cancellationToken, _limits.RequestTimeoutSeconds);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var history = await LoadHistoryAsync(conversationId, deadline.Token);
            var context = await BuildContextAsync(conversation, userId, request.Message, deadline.Token);
            var completion = await _aiService.GetChatResponseAsync(history, deadline.Token, context?.Prompt, new CampaignToolScope(userId, conversation.CampaignId!.Value));
            stopwatch.Stop();

            var assistantMessage = CreateAssistantMessage(conversationId, completion);
            ApplyContext(assistantMessage, context);
            _db.AIMessages.Add(assistantMessage);
            conversation.UpdatedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);

            return Ok(new ChatResponse(
                completion.Reply,
                completion.Model,
                conversationId,
                assistantMessage.Id,
                new AIUsageDto(
                    completion.Usage.InputTokens,
                    completion.Usage.OutputTokens,
                    completion.Usage.TotalTokens,
                    completion.Usage.EstimatedCostUsd), context?.Sources,
                context?.EmbeddingInputTokens ?? 0, context?.Warning));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException) when (deadline.IsCancellationRequested)
        {
            return StatusCode(StatusCodes.Status504GatewayTimeout, new { error = "The AI request timed out. Please try again." });
        }
        catch (AIBusyException ex)
        {
            Response.Headers.RetryAfter = "5";
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = ex.Message });
        }
        catch (AIRequestLimitException ex)
        {
            return UnprocessableEntity(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "AI request failed for conversation {ConversationId}.", conversationId);
            return Problem(
                statusCode: StatusCodes.Status502BadGateway,
                title: "AI provider unavailable",
                detail: "The AI provider could not complete the request. Please try again.");
        }
    }

    [HttpPost("conversations/{conversationId:guid}/messages/stream")]
    [Produces("text/event-stream")]
    public async Task StreamMessage(
        Guid conversationId,
        [FromBody] SendMessageRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryValidateMessage(request.Message, out var validationError))
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            await WriteSseAsync("error", new { error = validationError }, cancellationToken);
            return;
        }

        var userId = GetUserId();
        var conversation = await LoadConversationAsync(conversationId, userId, cancellationToken);

        if (conversation is null)
        {
            Response.StatusCode = StatusCodes.Status404NotFound;
            await WriteSseAsync("error", new { error = "Conversation not found." }, cancellationToken);
            return;
        }

        if (!_aiService.IsAvailable)
        {
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            await Response.WriteAsJsonAsync(new { error = AIServiceRegistration.UnavailableMessage }, cancellationToken);
            return;
        }

        await using var operation = await ConversationOperation.TryAcquireAsync(_db, conversationId, cancellationToken);
        if (operation is null)
        {
            Response.StatusCode = StatusCodes.Status409Conflict;
            Response.ContentType = "application/json";
            await Response.WriteAsJsonAsync(new { error = ConversationBusy }, cancellationToken);
            return;
        }
        await _db.Entry(conversation).ReloadAsync(cancellationToken);

        Response.StatusCode = StatusCodes.Status200OK;
        Response.ContentType = "text/event-stream; charset=utf-8";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Append("X-Accel-Buffering", "no");

        var userMessage = CreateUserMessage(conversationId, request.Message);
        _db.AIMessages.Add(userMessage);
        UpdateConversation(conversation, request.Message);
        await _db.SaveChangesAsync(cancellationToken);

        using var deadline = _limits.Deadline(cancellationToken, _limits.RequestTimeoutSeconds);
        var assistantText = new StringBuilder();

        try
        {
            var history = await LoadHistoryAsync(conversationId, deadline.Token);
            var context = await BuildContextAsync(conversation, userId, request.Message, deadline.Token);
            await foreach (var update in _aiService.StreamChatResponseAsync(history, deadline.Token, context?.Prompt, new CampaignToolScope(userId, conversation.CampaignId!.Value)))
            {
                if (update.Type == "token" && update.Text is not null)
                {
                    assistantText.Append(update.Text);
                    await WriteSseAsync("token", new { text = update.Text }, cancellationToken);
                }
                else if (update.Type == "completed" && update.Completion is not null)
                {
                    var completion = update.Completion;
                    var assistantMessage = CreateAssistantMessage(conversationId, completion);
                    ApplyContext(assistantMessage, context);
                    assistantMessage.Content = assistantText.Length > 0
                        ? assistantText.ToString()
                        : completion.Reply;

                    _db.AIMessages.Add(assistantMessage);
                    conversation.UpdatedAtUtc = DateTime.UtcNow;
                    await _db.SaveChangesAsync(cancellationToken);

                    await WriteSseAsync(
                        "done",
                        new
                        {
                            conversationId,
                            messageId = assistantMessage.Id,
                            model = completion.Model,
                            durationMs = completion.DurationMs,
                            reply = assistantMessage.Content,
                            sources = context?.Sources ?? [],
                            retrievalInputTokens = context?.EmbeddingInputTokens ?? 0,
                            retrievalWarning = context?.Warning,
                            usage = new AIUsageDto(
                                completion.Usage.InputTokens,
                                completion.Usage.OutputTokens,
                                completion.Usage.TotalTokens,
                                completion.Usage.EstimatedCostUsd)
                        },
                        cancellationToken);
                    return;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("AI stream cancelled for conversation {ConversationId}.", conversationId);
        }
        catch (OperationCanceledException) when (deadline.IsCancellationRequested)
        {
            await WriteSseAsync("error", new { error = "The AI request timed out. Please try again." }, cancellationToken);
        }
        catch (AIBusyException ex)
        {
            await WriteSseAsync("error", new { error = ex.Message }, cancellationToken);
        }
        catch (AIRequestLimitException ex)
        {
            await WriteSseAsync("error", new { error = ex.Message }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI streaming request failed for conversation {ConversationId}.", conversationId);
            await WriteSseAsync(
                "error",
                new { error = "The AI provider could not complete the request. Please try again." },
                CancellationToken.None);
        }
    }

    private async Task<AIConversation?> LoadConversationAsync(
        Guid conversationId,
        int userId,
        CancellationToken cancellationToken)
    {
        return await AccessibleConversations(userId).SingleOrDefaultAsync(x => x.Id == conversationId, cancellationToken);
    }

    private IQueryable<AIConversation> AccessibleConversations(int userId)
    {
        return _db.AIConversations.Where(x => x.UserId == userId && x.CampaignId != null &&
            _db.Campaigns.AccessibleTo(userId).Any(c => c.Id == x.CampaignId));
    }

    private Task<CampaignContext?> BuildContextAsync(AIConversation conversation, int userId, string question, CancellationToken ct)
        => conversation.CampaignId is int campaignId ? BuildCampaignContextAsync(campaignId, userId, question, ct)
            : Task.FromResult<CampaignContext?>(null);

    private async Task<CampaignContext?> BuildCampaignContextAsync(int campaignId, int userId, string question, CancellationToken ct)
        => await _knowledge.BuildContextAsync(campaignId, userId, question, ct);

    private static void ApplyContext(AIMessage message, CampaignContext? context)
    {
        if (context is null) return;
        message.SourcesJson = JsonSerializer.Serialize(context.Sources);
        message.RetrievalInputTokens = context.EmbeddingInputTokens;
        message.RetrievalWarning = context.Warning;
    }

    private async Task<List<AIMessage>> LoadHistoryAsync(
        Guid conversationId,
        CancellationToken cancellationToken)
    {
        return await _db.AIMessages
            .Where(x => x.ConversationId == conversationId && x.Status == "completed")
            .OrderByDescending(x => x.CreatedAtUtc).ThenByDescending(x => x.Id)
            .Take(30)
            .OrderBy(x => x.CreatedAtUtc).ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);
    }

    private static AIMessage CreateUserMessage(Guid conversationId, string content) => new()
    {
        ConversationId = conversationId,
        Role = "user",
        Content = content.Trim(),
        CreatedAtUtc = DateTime.UtcNow,
        Status = "completed"
    };

    private static AIMessage CreateAssistantMessage(
        Guid conversationId,
        AICompletionResult completion) => new()
    {
        ConversationId = conversationId,
        Role = "assistant",
        Content = completion.Reply,
        CreatedAtUtc = DateTime.UtcNow,
        Model = completion.Model,
        ResponseId = completion.ResponseId,
        InputTokenCount = completion.Usage.InputTokens,
        OutputTokenCount = completion.Usage.OutputTokens,
        TotalTokenCount = completion.Usage.TotalTokens,
        EstimatedCostUsd = completion.Usage.EstimatedCostUsd,
        DurationMs = completion.DurationMs,
        Status = "completed"
    };

    private static void UpdateConversation(AIConversation conversation, string firstMessage)
    {
        if (conversation.Title == "New AI Conversation")
        {
            var title = firstMessage.Trim().ReplaceLineEndings(" ");
            conversation.Title = title.Length > 80 ? $"{title[..80]}..." : title;
        }

        conversation.UpdatedAtUtc = DateTime.UtcNow;
    }

    private static bool TryValidateMessage(string? message, out string error)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            error = "Message cannot be empty.";
            return false;
        }

        if (message.Length > MaxMessageLength)
        {
            error = $"Message cannot exceed {MaxMessageLength} characters.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private async Task WriteSseAsync(
        string eventName,
        object payload,
        CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(payload, SseJsonOptions);
        var data = Encoding.UTF8.GetBytes($"event: {eventName}\ndata: {json}\n\n");
        await Response.Body.WriteAsync(data, cancellationToken);
        await Response.Body.FlushAsync(cancellationToken);
    }

    private int GetUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(claim, out var userId))
            throw new UnauthorizedAccessException("User identity is invalid.");
        return userId;
    }
}

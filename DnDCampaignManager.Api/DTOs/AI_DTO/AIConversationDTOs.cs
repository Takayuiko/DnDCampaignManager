namespace DnDCampaignManager.Api.DTOs.AI_DTO;

public sealed record CreateConversationRequest(string? Title, int? CampaignId = null);

public sealed record ConversationSummaryDto(
    Guid Id,
    string Title,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    int MessageCount,
    int? CampaignId = null);

public sealed record ConversationMessageDto(
    long Id,
    string Role,
    string Content,
    DateTime CreatedAtUtc,
    string? Model,
    long InputTokenCount,
    long OutputTokenCount,
    long TotalTokenCount,
    decimal EstimatedCostUsd,
    long DurationMs,
    string Status,
    IReadOnlyList<KnowledgeSourceDto>? Sources = null,
    int RetrievalInputTokens = 0,
    string? RetrievalWarning = null);

public sealed record ConversationDto(
    Guid Id,
    string Title,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    IReadOnlyList<ConversationMessageDto> Messages,
    int? CampaignId = null);

public sealed record SendMessageRequest(string Message);

public sealed record AIUsageDto(
    long InputTokens,
    long OutputTokens,
    long TotalTokens,
    decimal EstimatedCostUsd);

public sealed record ChatResponse(
    string Reply,
    string Model,
    Guid ConversationId,
    long MessageId,
    AIUsageDto Usage,
    IReadOnlyList<KnowledgeSourceDto>? Sources = null,
    int RetrievalInputTokens = 0,
    string? RetrievalWarning = null);

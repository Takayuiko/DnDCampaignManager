namespace DnDCampaignManager.Api.DTOs.AI_DTO;

public sealed record CreateConversationRequest(string? Title);

public sealed record ConversationSummaryDto(
    Guid Id,
    string Title,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    int MessageCount);

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
    string Status);

public sealed record ConversationDto(
    Guid Id,
    string Title,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    IReadOnlyList<ConversationMessageDto> Messages);

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
    AIUsageDto Usage);

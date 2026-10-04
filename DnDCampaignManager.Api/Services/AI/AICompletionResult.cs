namespace DnDCampaignManager.Api.Services.AI;

public sealed record AIUsage(
    long InputTokens,
    long OutputTokens,
    long TotalTokens,
    decimal EstimatedCostUsd);

public sealed record AICompletionResult(
    string Reply,
    string Model,
    string? ResponseId,
    AIUsage Usage,
    long DurationMs);

public sealed record AIStreamEvent(
    string Type,
    string? Text = null,
    AICompletionResult? Completion = null);

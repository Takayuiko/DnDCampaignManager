using System.ComponentModel.DataAnnotations;

namespace DnDCampaignManager.Api.DTOs.AI_DTO;

public sealed record CreateSessionNoteRequest(
    [Range(1, 100000)] int SessionNumber,
    [Required, StringLength(160)] string Title,
    [Required, StringLength(240000)] string Content,
    DateOnly PlayedOn);

public sealed record SessionNoteDto(long Id, int CampaignId, int SessionNumber, string Title,
    string Content, DateOnly PlayedOn, DateTime CreatedAtUtc, string IndexStatus,
    string? EmbeddingModel, int EmbeddingDimensions, int EmbeddingInputTokens, int ChunkCount);

public sealed record KnowledgeSourceDto(string Label, long SessionNoteId, int SessionNumber,
    string Title, string Excerpt, double Score, long? MapId = null);

public sealed record CampaignContext(string Prompt, IReadOnlyList<KnowledgeSourceDto> Sources,
    int EmbeddingInputTokens, string? Warning = null);

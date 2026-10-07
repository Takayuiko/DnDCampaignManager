using DnDCampingManager.Api.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace DnDCampaignManager.Api.Services.AI;

public sealed record CampaignToolScope(int UserId, int CampaignId);
public sealed record CampaignStateToolResult(int Id, string Name, string Description, int CharacterCount, int SessionNoteCount);
public sealed record CharacterSummaryToolResult(int Id, string Name, string Class, string Race, int Level);
public sealed record CharacterToolResult(int Id, string Name, string Class, string Race, int Level,
    int Strength, int Dexterity, int Constitution, int Intelligence, int Wisdom, int Charisma,
    int ArmorClass, int Speed, int HitPointMax, int HitPointCurrent, int HitPointTemporary);

// The only application data operations available to the model and MCP clients in this first slice.
public sealed class CampaignToolService(DnDxDbContext db, CampaignKnowledgeService knowledge)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private async Task RequireAccessAsync(CampaignToolScope scope, CancellationToken ct)
    {
        if (!await knowledge.CanAccessAsync(scope.CampaignId, scope.UserId, ct))
            throw new UnauthorizedAccessException("Campaign not found or access denied.");
    }

    public async Task<CampaignStateToolResult> GetCampaignStateAsync(CampaignToolScope scope, CancellationToken ct)
    {
        await RequireAccessAsync(scope, ct);
        return await db.Campaigns.AsNoTracking().Where(c => c.Id == scope.CampaignId)
            .Select(c => new CampaignStateToolResult(c.Id, c.Name, c.Description,
                c.Characters.Count, db.CampaignSessionNotes.Count(n => n.CampaignId == c.Id)))
            .SingleAsync(ct);
    }

    public async Task<IReadOnlyList<CharacterSummaryToolResult>> ListCharactersAsync(CampaignToolScope scope, CancellationToken ct)
    {
        await RequireAccessAsync(scope, ct);
        // Same shared campaign facts as RAG; account records and personal chats are never returned.
        return await db.Characters.AsNoTracking().Where(c => c.CampaignId == scope.CampaignId)
            .OrderBy(c => c.Id).Take(100)
            .Select(c => new CharacterSummaryToolResult(c.Id, c.Name, c.Class, c.Race, c.Level)).ToListAsync(ct);
    }

    public async Task<CharacterToolResult?> GetCharacterAsync(CampaignToolScope scope, int characterId, CancellationToken ct)
    {
        await RequireAccessAsync(scope, ct);
        return await db.Characters.AsNoTracking().Where(c => c.Id == characterId && c.CampaignId == scope.CampaignId)
            .Select(c => new CharacterToolResult(c.Id, c.Name, c.Class, c.Race, c.Level,
                c.Strength, c.Dexterity, c.Constitution, c.Intelligence, c.Wisdom, c.Charisma,
                c.ArmorClass, c.Speed, c.HitPointMax, c.HitPointCurrent, c.HitPointTemporary)).SingleOrDefaultAsync(ct);
    }

    public async Task<string> ExecuteAsync(CampaignToolScope scope, string name, string arguments, CancellationToken ct)
    {
        // Strict schemas assist the model, but application validation remains authoritative.
        try
        {
            if (arguments.Length > 2000) return Error("Tool arguments are too long.");
            using var document = JsonDocument.Parse(arguments);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return Error("Invalid tool arguments.");
            var properties = root.EnumerateObject().Select(p => p.Name).ToArray();
            object? result;
            switch (name)
            {
                case "GetCampaignState" when properties.Length == 0:
                    result = await GetCampaignStateAsync(scope, ct); break;
                case "ListCharacters" when properties.Length == 0:
                    result = await ListCharactersAsync(scope, ct); break;
                case "GetCharacter" when properties.Length == 1 && properties[0] == "characterId" &&
                    root.GetProperty("characterId").ValueKind == JsonValueKind.Number &&
                    root.GetProperty("characterId").TryGetInt32(out var id) && id > 0:
                    result = await GetCharacterAsync(scope, id, ct);
                    if (result is null) return Error("Character not found in this campaign.");
                    break;
                default: return Error("Unknown tool or invalid arguments.");
            }
            return JsonSerializer.Serialize(result, JsonOptions);
        }
        catch (JsonException) { return Error("Invalid tool arguments."); }
    }

    private static string Error(string message) => JsonSerializer.Serialize(new { error = message }, JsonOptions);
}

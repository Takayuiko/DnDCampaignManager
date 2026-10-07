using ModelContextProtocol;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Security.Claims;
using System.Text.Json;

namespace DnDCampaignManager.Api.Services.AI;

[McpServerToolType]
public sealed class CampaignMcpTools
{
    [McpServerTool(Name = "GetCampaignState", ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Read shared campaign details and character/session counts. Requires campaign membership or ownership.")]
    public static Task<string> GetCampaignState(CampaignToolService tools, ClaimsPrincipal user,
        [Description("The campaign to read.")] int campaignId, CancellationToken cancellationToken)
        => ExecuteAsync(tools, user, campaignId, "GetCampaignState", "{}", cancellationToken);

    [McpServerTool(Name = "ListCharacters", ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("List up to 100 shared characters in an accessible campaign. Does not return user account data.")]
    public static Task<string> ListCharacters(CampaignToolService tools, ClaimsPrincipal user,
        int campaignId, CancellationToken cancellationToken)
        => ExecuteAsync(tools, user, campaignId, "ListCharacters", "{}", cancellationToken);

    [McpServerTool(Name = "GetCharacter", ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Read current character stats in an accessible campaign. The character must belong to that campaign.")]
    public static Task<string> GetCharacter(CampaignToolService tools, ClaimsPrincipal user,
        int campaignId, int characterId, CancellationToken cancellationToken)
        => ExecuteAsync(tools, user, campaignId, "GetCharacter", JsonSerializer.Serialize(new { characterId }), cancellationToken);

    [McpServerTool(Name = "GetCharacterInventory", ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Read current inventory assignments, quantities and notes. Requires the character's player or campaign DM. Omitted entries are reported.")]
    public static Task<string> GetCharacterInventory(CampaignToolService tools, ClaimsPrincipal user,
        int campaignId, int characterId, CancellationToken cancellationToken)
        => ExecuteAsync(tools, user, campaignId, "GetCharacterInventory", JsonSerializer.Serialize(new { characterId }), cancellationToken);

    private static async Task<string> ExecuteAsync(CampaignToolService tools, ClaimsPrincipal user,
        int campaignId, string name, string arguments, CancellationToken ct)
    {
        if (user.Identity?.IsAuthenticated != true ||
            !int.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) || campaignId <= 0)
            throw new McpException("An authenticated user and valid campaign are required.");
        try { return await tools.ExecuteAsync(new(userId, campaignId), name, arguments, ct); }
        catch (UnauthorizedAccessException) { throw new McpException("Campaign not found or access denied."); }
    }
}

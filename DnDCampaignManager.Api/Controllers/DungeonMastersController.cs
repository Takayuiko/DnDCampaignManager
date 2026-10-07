using DnDCampaignManager.Api.DTOs;
using DnDCampaignManager.Api.Models;
using DnDCampaignManager.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace DnDCampaignManager.Api.Controllers;

[ApiController]
[Authorize(Roles = Roles.Admin)]
[Route("api/admin/dungeon-masters")]
public sealed class DungeonMastersController(DungeonMasterManagementService management) : ControllerBase
{
    private int ActorId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private async Task<IActionResult> Execute(Func<Task<IActionResult>> operation)
    {
        try { return await operation(); }
        catch (DmManagementException ex) { return StatusCode(ex.Status, ex.Message); }
    }

    [HttpGet]
    public Task<IActionResult> List(CancellationToken ct) => Execute(async () => Ok(await management.ListAsync(ActorId, ct)));

    [HttpPost]
    public Task<IActionResult> Promote(PromoteDungeonMasterDto request, CancellationToken ct) =>
        Execute(async () => Ok(await management.PromoteAsync(ActorId, request.Email, ct)));

    [HttpGet("{userId:int}/removal-preview")]
    public Task<IActionResult> Preview(int userId, CancellationToken ct) => Execute(async () => Ok(await management.PreviewAsync(ActorId, userId, ct)));

    [HttpDelete("{userId:int}")]
    public Task<IActionResult> Remove(int userId, [FromBody] RemoveDungeonMasterDto request, CancellationToken ct) => Execute(async () =>
    {
        await management.RemoveAsync(ActorId, userId, request.ExpectedCampaignCount, ct);
        return NoContent();
    });
}

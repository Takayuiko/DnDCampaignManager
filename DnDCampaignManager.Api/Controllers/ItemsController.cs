using System.Security.Claims;
using DnDCampaignManager.Api.DTOs;
using DnDCampaignManager.Api.Models;
using DnDCampaignManager.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DnDCampaignManager.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/campaigns/{campaignId:int}")]
public sealed class ItemsController(ItemService items) : ControllerBase
{
    private int UserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private bool IsDm => User.IsInRole(Roles.DM);
    private async Task<IActionResult> Execute(Func<Task<IActionResult>> operation)
    {
        try { return await operation(); }
        catch (ItemException ex) { return StatusCode(ex.Status, ex.Message); }
    }

    [HttpGet("items")]
    public Task<IActionResult> List(int campaignId, CancellationToken ct) => Execute(async () =>
        Ok(await items.ListAsync(campaignId, UserId, IsDm, ct)));

    [HttpPost("items"), Authorize(Roles = Roles.DM)]
    public Task<IActionResult> Create(int campaignId, SaveItemRequest request, CancellationToken ct) => Execute(async () =>
    {
        var item = await items.SaveAsync(campaignId, null, UserId, IsDm, request, ct);
        return Created($"/api/campaigns/{campaignId}/items/{item.Id}", ItemService.ItemResponse(item));
    });

    [HttpGet("items/{itemId:int}")]
    public Task<IActionResult> Get(int campaignId, int itemId, CancellationToken ct) => Execute(async () =>
    {
        var catalog = await items.ListAsync(campaignId, UserId, IsDm, ct);
        var item = catalog.Items.SingleOrDefault(x => x.Id == itemId);
        return item is null ? NotFound("Item not found in this campaign.") : Ok(item);
    });

    [HttpPut("items/{itemId:int}"), Authorize(Roles = Roles.DM)]
    public Task<IActionResult> Update(int campaignId, int itemId, SaveItemRequest request, CancellationToken ct) => Execute(async () =>
        Ok(ItemService.ItemResponse(await items.SaveAsync(campaignId, itemId, UserId, IsDm, request, ct))));

    [HttpDelete("items/{itemId:int}"), Authorize(Roles = Roles.DM)]
    public Task<IActionResult> Delete(int campaignId, int itemId, CancellationToken ct) => Execute(async () =>
    {
        await items.DeleteAsync(campaignId, itemId, UserId, IsDm, ct);
        return NoContent();
    });

    [HttpPost("items/import-srd"), Authorize(Roles = Roles.DM)]
    public Task<IActionResult> Import(int campaignId, CancellationToken ct) => Execute(async () =>
        Ok(await items.ImportAsync(campaignId, UserId, IsDm, ct)));

    [HttpGet("items/deletion-preview"), Authorize(Roles = Roles.DM)]
    public Task<IActionResult> DeletionPreview(int campaignId, CancellationToken ct) => Execute(async () =>
        Ok(await items.DeleteAllPreviewAsync(campaignId, UserId, IsDm, ct)));

    [HttpDelete("items"), Authorize(Roles = Roles.DM)]
    public Task<IActionResult> DeleteAll(int campaignId, [FromBody] DeleteAllItemsRequest request, CancellationToken ct) => Execute(async () =>
        Ok(await items.DeleteAllAsync(campaignId, UserId, IsDm, request, ct)));

    [HttpGet("characters/{characterId:int}/inventory")]
    public Task<IActionResult> Inventory(int campaignId, int characterId, CancellationToken ct) => Execute(async () =>
        Ok(await items.InventoryAsync(campaignId, characterId, UserId, IsDm, ct)));

    [HttpPost("characters/{characterId:int}/inventory"), Authorize(Roles = Roles.DM)]
    public Task<IActionResult> Assign(int campaignId, int characterId, AssignItemRequest request, CancellationToken ct) => Execute(async () =>
        StatusCode(201, ItemService.AssignmentResponse(await items.AssignAsync(campaignId, characterId, UserId, IsDm, request, ct))));
}

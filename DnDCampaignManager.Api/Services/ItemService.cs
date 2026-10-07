using DnDCampaignManager.Api.DTOs;
using DnDCampaignManager.Api.Models;
using DnDCampingManager.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace DnDCampaignManager.Api.Services;

public sealed class ItemException(int status, string message) : Exception(message)
{
    public int Status { get; } = status;
}

public sealed class ItemService(DnDxDbContext db)
{
    private static ItemDto ToDto(CampaignItem item) => new(item.Id, item.Name, item.Category,
        item.Description, item.WeightLb, item.CostGp, item.Source);

    private async Task<Campaign> CampaignAsync(int campaignId, int userId, CancellationToken ct)
    {
        var campaign = await db.Campaigns.Include(x => x.Players).SingleOrDefaultAsync(x => x.Id == campaignId, ct)
            ?? throw new ItemException(404, "Campaign not found.");
        if (!(campaign.OwnerId == userId || campaign.Players.Any(x => x.UserId == userId)))
            throw new ItemException(403, "You do not have permission to access these items.");
        return campaign;
    }

    // Serialize catalog mutations/assignments within a campaign, including duplicate imports
    // and assignment versus deletion. Reuse the surrounding transaction in integration checks.
    private async Task<T> MutateAsync<T>(int campaignId, int userId, bool isDm,
        Func<Task<T>> operation, CancellationToken ct)
    {
        await using var transaction = db.Database.CurrentTransaction is null
            ? await db.Database.BeginTransactionAsync(ct) : null;
        var campaign = await db.Campaigns.FromSqlInterpolated(
            $"SELECT * FROM \"Campaigns\" WHERE \"Id\" = {campaignId} FOR UPDATE").SingleOrDefaultAsync(ct)
            ?? throw new ItemException(404, "Campaign not found.");
        if (!isDm || campaign.OwnerId != userId)
            throw new ItemException(403, "Only this campaign's DM can manage or assign items.");
        var result = await operation();
        await db.SaveChangesAsync(ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
        return result;
    }

    public async Task<ItemCatalogDto> ListAsync(int campaignId, int userId, bool isDm, CancellationToken ct)
    {
        var campaign = await CampaignAsync(campaignId, userId, ct);
        var items = await db.CampaignItems.AsNoTracking().Where(x => x.CampaignId == campaignId)
            .OrderBy(x => x.Name).ToListAsync(ct);
        return new(isDm && campaign.OwnerId == userId, items.Select(ToDto).ToList());
    }

    public Task<CampaignItem> SaveAsync(int campaignId, int? itemId, int userId, bool isDm,
        SaveItemRequest request, CancellationToken ct) => MutateAsync(campaignId, userId, isDm, async () =>
    {
        var name = request.Name.Trim();
        var category = request.Category.Trim();
        if (name.Length == 0 || category.Length == 0) throw new ItemException(400, "Name and category are required.");
        var normalized = name.ToUpperInvariant();
        if (await db.CampaignItems.AnyAsync(x => x.CampaignId == campaignId &&
            x.NormalizedName == normalized && (!itemId.HasValue || x.Id != itemId.Value), ct))
            throw new ItemException(409, "An item with that name already exists in this campaign.");
        var item = itemId.HasValue
            ? await db.CampaignItems.SingleOrDefaultAsync(x => x.Id == itemId && x.CampaignId == campaignId, ct)
                ?? throw new ItemException(404, "Item not found in this campaign.")
            : new CampaignItem { CampaignId = campaignId };
        item.Name = name;
        item.NormalizedName = normalized;
        item.Category = category;
        item.Description = request.Description.Trim();
        item.WeightLb = request.WeightLb;
        item.CostGp = request.CostGp;
        if (!itemId.HasValue) db.CampaignItems.Add(item);
        return item;
    }, ct);

    public Task<bool> DeleteAsync(int campaignId, int itemId, int userId, bool isDm, CancellationToken ct) =>
        MutateAsync(campaignId, userId, isDm, async () =>
        {
            var item = await db.CampaignItems.SingleOrDefaultAsync(x => x.Id == itemId && x.CampaignId == campaignId, ct)
                ?? throw new ItemException(404, "Item not found in this campaign.");
            if (await db.CharacterItems.AnyAsync(x => x.ItemId == itemId, ct))
                throw new ItemException(409, "This item is assigned to a character and cannot be deleted.");
            db.CampaignItems.Remove(item);
            return true;
        }, ct);

    public async Task<DeleteAllItemsPreview> DeleteAllPreviewAsync(int campaignId, int userId, bool isDm, CancellationToken ct)
    {
        var campaign = await CampaignAsync(campaignId, userId, ct);
        if (!isDm || campaign.OwnerId != userId)
            throw new ItemException(403, "Only this campaign's DM can delete all items.");
        return new(await db.CampaignItems.CountAsync(x => x.CampaignId == campaignId, ct),
            await db.CharacterItems.CountAsync(x => x.CampaignId == campaignId, ct));
    }

    public Task<DeleteAllItemsPreview> DeleteAllAsync(int campaignId, int userId, bool isDm,
        DeleteAllItemsRequest request, CancellationToken ct) => MutateAsync(campaignId, userId, isDm, async () =>
        {
            var itemCount = await db.CampaignItems.CountAsync(x => x.CampaignId == campaignId, ct);
            var inventoryCount = await db.CharacterItems.CountAsync(x => x.CampaignId == campaignId, ct);
            if (itemCount != request.ExpectedItemCount || inventoryCount != request.ExpectedInventoryEntryCount)
                throw new ItemException(409, "The campaign's items changed. Review the deletion counts again before confirming.");
            // Delete dependent entries first. Both deletions share the campaign write transaction.
            await db.CharacterItems.Where(x => x.CampaignId == campaignId).ExecuteDeleteAsync(ct);
            await db.CampaignItems.Where(x => x.CampaignId == campaignId).ExecuteDeleteAsync(ct);
            return new DeleteAllItemsPreview(itemCount, inventoryCount);
        }, ct);

    public Task<ImportItemsResult> ImportAsync(int campaignId, int userId, bool isDm, CancellationToken ct) =>
        MutateAsync(campaignId, userId, isDm, async () =>
        {
            var existing = await db.CampaignItems.Where(x => x.CampaignId == campaignId)
                .ToDictionaryAsync(x => x.NormalizedName, ct);
            var added = 0;
            var updated = 0;
            foreach (var starter in SrdItemCatalog.Items)
            {
                var normalized = starter.Name.ToUpperInvariant();
                if (!existing.TryGetValue(normalized, out var item))
                {
                    db.CampaignItems.Add(new CampaignItem { CampaignId = campaignId, Name = starter.Name,
                        NormalizedName = normalized, Category = starter.Category, Source = SrdItemCatalog.Source,
                        CostGp = starter.CostGp, WeightLb = starter.WeightLb, Description = starter.Description });
                    added++;
                }
                else if (item.Source == SrdItemCatalog.Source)
                {
                    // Upgrade names-only imports without overwriting DM values.
                    var changed = !item.CostGp.HasValue || !item.WeightLb.HasValue ||
                        (item.Description.Length == 0 && starter.Description.Length > 0);
                    item.CostGp ??= starter.CostGp;
                    item.WeightLb ??= starter.WeightLb;
                    if (item.Description.Length == 0) item.Description = starter.Description;
                    if (changed) updated++;
                }
            }
            return new ImportItemsResult(added, updated);
        }, ct);

    public async Task<CharacterInventoryDto> InventoryAsync(int campaignId, int characterId, int userId, bool isDm, CancellationToken ct)
    {
        var campaign = await CampaignAsync(campaignId, userId, ct);
        var character = await db.Characters.SingleOrDefaultAsync(x => x.Id == characterId && x.CampaignId == campaignId, ct)
            ?? throw new ItemException(404, "Character not found in this campaign.");
        var canAssign = isDm && campaign.OwnerId == userId;
        if (!canAssign && character.UserId != userId) throw new ItemException(403, "You cannot view this character's inventory.");
        var entries = await db.CharacterItems.AsNoTracking().Include(x => x.Item)
            .Where(x => x.CampaignId == campaignId && x.CharacterId == characterId)
            .OrderBy(x => x.Item.Name).ThenBy(x => x.Id).ToListAsync(ct);
        return new(canAssign, entries.Select(x => new InventoryItemDto(x.Id, ToDto(x.Item), x.Quantity, x.Notes, x.AssignedAtUtc)).ToList());
    }

    public Task<CharacterItem> AssignAsync(int campaignId, int characterId, int userId, bool isDm,
        AssignItemRequest request, CancellationToken ct) => MutateAsync(campaignId, userId, isDm, async () =>
    {
        if (!await db.Characters.AnyAsync(x => x.Id == characterId && x.CampaignId == campaignId, ct))
            throw new ItemException(404, "Character not found in this campaign.");
        var item = await db.CampaignItems.SingleOrDefaultAsync(x => x.Id == request.ItemId && x.CampaignId == campaignId, ct)
            ?? throw new ItemException(404, "Item not found in this campaign.");
        var entry = new CharacterItem { CampaignId = campaignId, CharacterId = characterId,
            ItemId = item.Id, Item = item, Quantity = request.Quantity, Notes = request.Notes.Trim() };
        db.CharacterItems.Add(entry);
        return entry;
    }, ct);

    public static ItemDto ItemResponse(CampaignItem item) => ToDto(item);
    public static InventoryItemDto AssignmentResponse(CharacterItem entry) =>
        new(entry.Id, ToDto(entry.Item), entry.Quantity, entry.Notes, entry.AssignedAtUtc);
}

using System.ComponentModel.DataAnnotations;

namespace DnDCampaignManager.Api.DTOs;

public sealed record SaveItemRequest(
    [Required, StringLength(120)] string Name,
    [Required, StringLength(50)] string Category,
    [Required(AllowEmptyStrings = true), StringLength(4000)] string Description,
    [Range(typeof(decimal), "0", "1000000")] decimal? WeightLb,
    [Range(typeof(decimal), "0", "1000000000")] decimal? CostGp);

public sealed record AssignItemRequest(
    [Range(1, int.MaxValue)] int ItemId,
    [Range(1, int.MaxValue)] int Quantity,
    [Required(AllowEmptyStrings = true), StringLength(2000)] string Notes);

public sealed record ItemDto(int Id, string Name, string Category, string Description,
    decimal? WeightLb, decimal? CostGp, string? Source);
public sealed record InventoryItemDto(int Id, ItemDto Item, int Quantity, string Notes, DateTime AssignedAtUtc);
public sealed record ItemCatalogDto(bool CanManage, List<ItemDto> Items);
public sealed record CharacterInventoryDto(bool CanAssign, List<InventoryItemDto> Items);
public sealed record ImportItemsResult(int Added, int Updated);
public sealed record DeleteAllItemsPreview(int ItemCount, int InventoryEntryCount);
public sealed record DeleteAllItemsRequest(
    [Range(0, int.MaxValue)] int ExpectedItemCount,
    [Range(0, int.MaxValue)] int ExpectedInventoryEntryCount);


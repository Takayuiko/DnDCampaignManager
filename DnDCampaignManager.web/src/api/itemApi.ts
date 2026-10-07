import api from "./axios";

export type ItemInput = {
    name: string; category: string; description: string; weightLb: number | null; costGp: number | null;
};
export type Item = ItemInput & { id: number; source: string | null };
export type InventoryItem = { id: number; item: Item; quantity: number; notes: string; assignedAtUtc: string };
export type ItemCatalog = { canManage: boolean; items: Item[] };
export type CharacterInventory = { canAssign: boolean; items: InventoryItem[] };
const catalogUrl = (campaignId: number) => `/campaigns/${campaignId}/items`;
const inventoryUrl = (campaignId: number, characterId: number) => `/campaigns/${campaignId}/characters/${characterId}/inventory`;
export const getItems = (campaignId: number, signal?: AbortSignal) => api.get<ItemCatalog>(catalogUrl(campaignId), { signal });
export const createItem = (campaignId: number, item: ItemInput) => api.post<Item>(catalogUrl(campaignId), item);
export const updateItem = (campaignId: number, id: number, item: ItemInput) => api.put<Item>(`${catalogUrl(campaignId)}/${id}`, item);
export const deleteItem = (campaignId: number, id: number) => api.delete(`${catalogUrl(campaignId)}/${id}`);
export type DeleteItemsPreview = { itemCount: number; inventoryEntryCount: number };
export const previewDeleteAllItems = (campaignId: number) => api.get<DeleteItemsPreview>(`${catalogUrl(campaignId)}/deletion-preview`);
export const deleteAllItems = (campaignId: number, preview: DeleteItemsPreview) => api.delete<DeleteItemsPreview>(catalogUrl(campaignId), {
    data: { expectedItemCount: preview.itemCount, expectedInventoryEntryCount: preview.inventoryEntryCount }
});
export const importSrdItems = (campaignId: number) => api.post<{ added: number; updated: number }>(`${catalogUrl(campaignId)}/import-srd`);
export const getInventory = (campaignId: number, characterId: number, signal?: AbortSignal) => api.get<CharacterInventory>(inventoryUrl(campaignId, characterId), { signal });
export const assignItem = (campaignId: number, characterId: number, request: { itemId: number; quantity: number; notes: string }) =>
    api.post<InventoryItem>(inventoryUrl(campaignId, characterId), request);

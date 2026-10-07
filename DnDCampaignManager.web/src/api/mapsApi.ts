import api from "./axios";

export type MapLocation = { name: string; description: string; x: number; y: number };
export type CampaignMap = {
    id: number; campaignId: number; title: string; description: string;
    locations: MapLocation[]; indexStatus: "pending" | "ready" | "failed";
};
export type MapDraft = Pick<CampaignMap, "title" | "description" | "locations">;
const path = (campaignId: number) => `/campaigns/${campaignId}/maps`;
export const getMaps = (campaignId: number, signal?: AbortSignal) => api.get<CampaignMap[]>(path(campaignId), { signal });
export const getMapImage = (campaignId: number, mapId: number, signal?: AbortSignal) =>
    api.get<Blob>(`${path(campaignId)}/${mapId}/image`, { responseType: "blob", signal, timeout: 180000 });
export const getMapThumbnail = (campaignId: number, mapId: number, signal?: AbortSignal) =>
    api.get<Blob>(`${path(campaignId)}/${mapId}/thumbnail`, { responseType: "blob", signal });
export function saveMap(campaignId: number, mapId: number | null, draft: MapDraft, image: File | null, signal?: AbortSignal) {
    const form = new FormData();
    form.append("title", draft.title); form.append("description", draft.description);
    form.append("locationsJson", JSON.stringify(draft.locations));
    if (image) form.append("image", image);
    return mapId === null ? api.post<CampaignMap>(path(campaignId), form, { timeout: 180000, signal })
        : api.put<CampaignMap>(`${path(campaignId)}/${mapId}`, form, { timeout: 180000, signal });
}
export const deleteMap = (campaignId: number, mapId: number, signal?: AbortSignal) => api.delete(`${path(campaignId)}/${mapId}`, { signal });
export const indexMap = (campaignId: number, mapId: number, signal?: AbortSignal) => api.post<CampaignMap>(`${path(campaignId)}/${mapId}/index`, undefined, { signal });

import api from "./axios";

export type CampaignNpc = {
    id: number; campaignId: number; name: string; description: string; isPartyMember: boolean;
    mapId: number | null; locationName: string; hasImage: boolean; version: string;
};
export type NpcDraft = { name: string; description: string; isPartyMember: boolean; mapId: number | null; locationName: string };
export type NpcCatalog = { canManage: boolean; npcs: CampaignNpc[] };
const path = (campaignId: number) => `/campaigns/${campaignId}/npcs`;
export const getNpcs = (campaignId: number, signal?: AbortSignal) => api.get<NpcCatalog>(path(campaignId), { signal });
export const getNpcImage = (campaignId: number, npcId: number, signal?: AbortSignal) =>
    api.get<Blob>(`${path(campaignId)}/${npcId}/image`, { signal, responseType: "blob" });
export const getNpcThumbnail = (campaignId: number, npcId: number, signal?: AbortSignal) =>
    api.get<Blob>(`${path(campaignId)}/${npcId}/thumbnail`, { signal, responseType: "blob" });
export function saveNpc(campaignId: number, npc: CampaignNpc | null, draft: NpcDraft, image: File | null, removeImage: boolean, signal?: AbortSignal) {
    const form = new FormData();
    form.append("name", draft.name); form.append("description", draft.description);
    form.append("mapId", String(draft.mapId ?? 0)); form.append("locationName", draft.locationName);
    form.append("isPartyMember", String(draft.isPartyMember)); form.append("removeImage", String(removeImage));
    if (image) form.append("image", image);
    if (npc) form.append("version", npc.version);
    return npc ? api.put<CampaignNpc>(`${path(campaignId)}/${npc.id}`, form, { signal })
        : api.post<CampaignNpc>(path(campaignId), form, { signal });
}
export const deleteNpc = (campaignId: number, npcId: number, signal?: AbortSignal) => api.delete(`${path(campaignId)}/${npcId}`, { signal });

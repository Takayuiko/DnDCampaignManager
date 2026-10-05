import api from "./axios";

export type DungeonMaster = { id: number; email: string; role: "DM"; isAdmin: boolean; campaignCount: number };
export type DmRemovalPreview = { userId: number; email: string; campaignCount: number; characterCount: number;
    sessionNoteCount: number; campaignConversationCount: number; generalConversationCount: number; preservedCharacterCount: number };

export const getDungeonMasters = () => api.get<DungeonMaster[]>("/admin/dungeon-masters");
export const promoteDungeonMaster = (email: string) => api.post<DungeonMaster>("/admin/dungeon-masters", { email });
export const previewDmRemoval = (id: number) => api.get<DmRemovalPreview>(`/admin/dungeon-masters/${id}/removal-preview`);
export const removeDungeonMaster = (id: number, expectedCampaignCount: number) =>
    api.delete(`/admin/dungeon-masters/${id}`, { data: { expectedCampaignCount } });

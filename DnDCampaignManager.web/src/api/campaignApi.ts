import api from "./axios";

export const getCampaigns = () => api.get("/campaigns");

export const getCampaign = (id: number) =>  api.get(`/campaigns/${id}`);

export const createCampaign = (data: { name: string; description: string; }) =>
    api.post("/campaigns", data);

export const deleteCampaign = (id: number) =>
    api.delete(`/campaigns/${id}`);

export function updateCampaign(id: number, data: { name: string; description: string }) {
    return api.put(`/campaigns/${id}`, data);
}
export function addPlayerToCampaign(campaignId: number, email: string) {
    return api.post(`/campaigns/${campaignId}/players`, { email });
}

export const removePlayerFromCampaign = (campaignId: number, playerId: number) =>
    api.delete(`/campaigns/${campaignId}/players/${playerId}`)

export const createCharacter = (campaignId: number, data: any) =>
    api.post(`/campaigns/${campaignId}/characters`, data);

export const getCharacter = (campaignId: number, characterId: number) =>
    api.get(`/campaigns/${campaignId}/characters/${characterId}`);

export const getCharactersByCampaign = (campaignId: number) =>
    api.get(`/campaigns/${campaignId}/characters`);

export const updateCharacterByCampaign = ( campaignId: number, characterId: number, data: any ) =>
    api.put(`/campaigns/${campaignId}/characters/${characterId}`, data);

// Character Classes
export const getCharacterClass = (campaignId: number) =>
    api.get(`/campaigns/${campaignId}/character-classes`);

export const addCharacterClass = (campaignId: number, data: { name: string; }) =>
    api.post(`/campaigns/${campaignId}/character-classes`, data);


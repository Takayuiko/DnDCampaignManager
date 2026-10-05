import api from "./axios";
import { type GetCharacterResponse, type CharacterForm } from "../components/Character/CharacterSheetForm";
import { toCharacterRequest } from "./characterRequest";

// Campaign
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
    return api.post<{ id: number; email: string }>(`/campaigns/${campaignId}/players`, { email });
}

// Character
export const removePlayerFromCampaign = (campaignId: number, playerId: number) =>
    api.delete(`/campaigns/${campaignId}/players/${playerId}`)

export const createCharacter = (campaignId: number, data: CharacterForm) =>
    api.post<GetCharacterResponse>(`/campaigns/${campaignId}/characters`, toCharacterRequest(data));

export const getCharacter = (campaignId: number, characterId: number) =>
    api.get<GetCharacterResponse>(`/campaigns/${campaignId}/characters/${characterId}`);

export const getCharactersByCampaign = (campaignId: number) =>
    api.get(`/campaigns/${campaignId}/characters`);

export const updateCharacterByCampaign = ( campaignId: number, characterId: number, data: CharacterForm ) =>
    api.put<void>(`/campaigns/${campaignId}/characters/${characterId}`, toCharacterRequest(data));

// Character Classes
export const getCharacterClass = (campaignId: number) =>
    api.get(`/campaigns/${campaignId}/character-classes`);

export const addCharacterClass = (campaignId: number, data: { name: string; }) =>
    api.post(`/campaigns/${campaignId}/character-classes`, data);

// Character Races
export const getCharacterRaces = (campaignId: number) =>
    api.get(`/campaigns/${campaignId}/character-races`);

export const addCharacterRace = (campaignId: number, data: { name: string }) =>
    api.post(`/campaigns/${campaignId}/character-races`, data);

// Character Backgrounds
export const getCharacterBackgrounds = (campaignId: number) =>
    api.get(`/campaigns/${campaignId}/character-backgrounds`);

export const addCharacterBackground = (campaignId: number, data: { name: string }) =>
    api.post(`/campaigns/${campaignId}/character-backgrounds`, data);

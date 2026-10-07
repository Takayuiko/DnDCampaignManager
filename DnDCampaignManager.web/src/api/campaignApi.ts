import api from "./axios";
import { type GetCharacterResponse, type CharacterForm, type CharacterOption } from "../components/Character/characterTypes";
import { toCharacterRequest } from "./characterRequest";

// Campaign
export type CampaignSummary = {
    id: number;
    name: string;
    description: string;
    ownerId: number;
    characters: { id: number; name: string; userId: number; class: string; race: string; level: number }[];
    players: { id: number; email: string }[];
};
export const getCampaigns = () => api.get<CampaignSummary[]>("/campaigns");

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
export type CharacterListItem = Pick<GetCharacterResponse,
    "id" | "userId" | "name" | "class" | "race" | "level" | "background" |
    "alignment" | "experiencePoints" | "strength" | "dexterity" | "constitution" |
    "intelligence" | "wisdom" | "charisma" | "proficiencyBonus" | "armorClass" |
    "initiative" | "speed" | "hitPointMax" | "hitPointCurrent" | "hitPointTemporary">;

export const removePlayerFromCampaign = (campaignId: number, playerId: number) =>
    api.delete(`/campaigns/${campaignId}/players/${playerId}`)

export const createCharacter = (campaignId: number, data: CharacterForm) =>
    api.post<GetCharacterResponse>(`/campaigns/${campaignId}/characters`, toCharacterRequest(data));

export const getCharacter = (campaignId: number, characterId: number) =>
    api.get<GetCharacterResponse>(`/campaigns/${campaignId}/characters/${characterId}`);

export const getCharactersByCampaign = (campaignId: number) =>
    api.get<CharacterListItem[]>(`/campaigns/${campaignId}/characters`);

export const updateCharacterByCampaign = ( campaignId: number, characterId: number, data: CharacterForm ) =>
    api.put<void>(`/campaigns/${campaignId}/characters/${characterId}`, toCharacterRequest(data));

// Character Classes
export const getCharacterClass = (campaignId: number) =>
    api.get<CharacterOption[]>(`/campaigns/${campaignId}/character-classes`);

export const addCharacterClass = (campaignId: number, data: { name: string; }) =>
    api.post<CharacterOption>(`/campaigns/${campaignId}/character-classes`, data);

// Character Races
export const getCharacterRaces = (campaignId: number) =>
    api.get<CharacterOption[]>(`/campaigns/${campaignId}/character-races`);

export const addCharacterRace = (campaignId: number, data: { name: string }) =>
    api.post<CharacterOption>(`/campaigns/${campaignId}/character-races`, data);

// Character Backgrounds
export const getCharacterBackgrounds = (campaignId: number) =>
    api.get<CharacterOption[]>(`/campaigns/${campaignId}/character-backgrounds`);

export const addCharacterBackground = (campaignId: number, data: { name: string }) =>
    api.post<CharacterOption>(`/campaigns/${campaignId}/character-backgrounds`, data);

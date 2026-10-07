import api from "./axios";

export type SessionNote = {
    id: number;
    campaignId: number;
    sessionNumber: number;
    title: string;
    content: string;
    playedOn: string;
    createdAtUtc: string;
    indexStatus: "pending" | "ready" | "failed";
    embeddingModel: string | null;
    embeddingDimensions: number;
    embeddingInputTokens: number;
    chunkCount: number;
};

export type SessionNoteRequest = Pick<SessionNote, "sessionNumber" | "title" | "content" | "playedOn">;
export const getSessionNotes = (campaignId: number, signal?: AbortSignal) =>
    api.get<SessionNote[]>(`/campaigns/${campaignId}/session-notes`, { signal });
export const createSessionNote = (campaignId: number, request: SessionNoteRequest, signal?: AbortSignal) =>
    api.post<SessionNote>(`/campaigns/${campaignId}/session-notes`, request, { timeout: 180000, signal });
export const indexSessionNote = (campaignId: number, noteId: number, signal?: AbortSignal) =>
    api.post<SessionNote>(`/campaigns/${campaignId}/session-notes/${noteId}/index`, undefined, { timeout: 180000, signal });
export const deleteSessionNote = (campaignId: number, noteId: number, signal?: AbortSignal) =>
    api.delete(`/campaigns/${campaignId}/session-notes/${noteId}`, { signal });

export const transcribeSessionAudio = (campaignId: number, audio: File, signal?: AbortSignal) => {
    const form = new FormData();
    form.append("audio", audio);
    return api.post<{ text: string; model: string }>(`/campaigns/${campaignId}/session-notes/audio/transcribe`, form,
        { timeout: 180000, signal });
};

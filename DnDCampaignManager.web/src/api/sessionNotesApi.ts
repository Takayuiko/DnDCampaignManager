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
export const getSessionNotes = (campaignId: number) =>
    api.get<SessionNote[]>(`/campaigns/${campaignId}/session-notes`);
export const createSessionNote = (campaignId: number, request: SessionNoteRequest) =>
    api.post<SessionNote>(`/campaigns/${campaignId}/session-notes`, request);
export const indexSessionNote = (campaignId: number, noteId: number) =>
    api.post<SessionNote>(`/campaigns/${campaignId}/session-notes/${noteId}/index`);
export const deleteSessionNote = (campaignId: number, noteId: number) =>
    api.delete(`/campaigns/${campaignId}/session-notes/${noteId}`);

export const transcribeSessionAudio = (campaignId: number, audio: File) => {
    const form = new FormData();
    form.append("audio", audio);
    return api.post<{ text: string; model: string }>(`/campaigns/${campaignId}/session-notes/audio/transcribe`, form,
        { timeout: 660000 });
};

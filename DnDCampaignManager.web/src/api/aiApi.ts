import api from "./axios";

export type ChatMessage = {
    role: "user" | "assistant";
    content: string;
};

export type ChatResponse = {
    reply: string;
    model: string;
};

export type AIStatus = {
    configured: boolean;
    provider: string;
    model: string;
};

export async function getAIStatus() {
    return api.get<AIStatus>("/ai/status");
}

export async function sendAIMessage(
    message: string,
    history: ChatMessage[]
) {
    return api.post<ChatResponse>("/ai/chat", {
        message,
        history
    });
}

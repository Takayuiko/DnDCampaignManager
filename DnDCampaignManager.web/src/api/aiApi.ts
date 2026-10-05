import api from "./axios";

export type ConversationSummary = {
    id: string;
    title: string;
    createdAtUtc: string;
    updatedAtUtc: string;
    messageCount: number;
    campaignId?: number | null;
};

export type KnowledgeSource = {
    label: string;
    sessionNoteId: number;
    sessionNumber: number;
    title: string;
    excerpt: string;
    score: number;
};

export type ConversationMessage = {
    id: number;
    role: "user" | "assistant";
    content: string;
    createdAtUtc: string;
    model?: string;
    inputTokenCount: number;
    outputTokenCount: number;
    totalTokenCount: number;
    estimatedCostUsd: number;
    durationMs: number;
    status: string;
    sources?: KnowledgeSource[];
    retrievalInputTokens?: number;
    retrievalWarning?: string | null;
};

export type Conversation = {
    id: string;
    title: string;
    createdAtUtc: string;
    updatedAtUtc: string;
    messages: ConversationMessage[];
    campaignId?: number | null;
};

export type AIStatus = {
    configured: boolean;
    provider: string;
    model: string;
};

export type AIUsage = {
    inputTokens: number;
    outputTokens: number;
    totalTokens: number;
    estimatedCostUsd: number;
};

export type StreamDoneEvent = {
    conversationId: string;
    messageId: number;
    model: string;
    usage: AIUsage;
    durationMs: number;
    reply: string;
    sources?: KnowledgeSource[];
    retrievalInputTokens?: number;
    retrievalWarning?: string | null;
};

export async function getAIStatus() {
    return api.get<AIStatus>("/ai/status");
}

export async function getConversations() {
    return api.get<ConversationSummary[]>("/ai/conversations");
}

export async function createConversation(title?: string, campaignId?: number) {
    return api.post<ConversationSummary>("/ai/conversations", {
        title: title ?? null,
        campaignId: campaignId ?? null
    });
}

export async function getDefaultConversation() {
    return api.post<ConversationSummary | undefined>("/ai/conversations/default");
}

export async function getConversation(conversationId: string) {
    return api.get<Conversation>(`/ai/conversations/${conversationId}`);
}

export async function deleteConversation(conversationId: string) {
    return api.delete<ConversationSummary | undefined>(`/ai/conversations/${conversationId}`);
}

async function fetchStream(
    conversationId: string,
    message: string,
    onToken: (text: string) => void,
    onDone: (event: StreamDoneEvent) => void,
    onError: (message: string) => void,
    retryAfterRefresh = true
) {
    const baseURL = (api.defaults.baseURL ?? "/api").replace(/\/$/, "");
    const url = `${baseURL}/ai/conversations/${conversationId}/messages/stream`;
    const token = localStorage.getItem("token");

    const response = await fetch(url, {
        method: "POST",
        headers: {
            "Content-Type": "application/json",
            ...(token ? { Authorization: `Bearer ${token}` } : {})
        },
        body: JSON.stringify({ message })
    });

    if (response.status === 401 && retryAfterRefresh) {
        try {
            const refresh = await api.post("/auth/refresh");
            const newToken = refresh.data.accessToken;
            localStorage.setItem("token", newToken);
            return fetchStream(
                conversationId,
                message,
                onToken,
                onDone,
                onError,
                false
            );
        } catch {
            throw new Error("Your session has expired.");
        }
    }

    if (!response.ok || !response.body) {
        let messageText = "The AI request failed.";
        try {
            const body = await response.json();
            messageText = body.error ?? body.detail ?? messageText;
        } catch {
            // Keep the generic message when the server did not return JSON.
        }
        throw new Error(messageText);
    }

    const reader = response.body.getReader();
    const decoder = new TextDecoder();
    let buffer = "";
    let terminal = false;

    const processEvent = (rawEvent: string) => {
        let eventName = "message";
        let data = "";

        for (const line of rawEvent.split(/\r?\n/)) {
            if (line.startsWith("event:")) {
                eventName = line.slice(6).trim();
            } else if (line.startsWith("data:")) {
                data += line.slice(5).trim();
            }
        }

        if (!data) return;

        const payload = JSON.parse(data);

        if (eventName === "token") {
            onToken(payload.text ?? "");
        } else if (eventName === "done") {
            if (!payload.usage ||
                ![payload.usage.inputTokens, payload.usage.outputTokens, payload.usage.totalTokens,
                    payload.usage.estimatedCostUsd, payload.durationMs].every(value =>
                    typeof value === "number" && Number.isFinite(value) && value >= 0) ||
                typeof payload.reply !== "string") {
                onError("The AI response could not be completed. Please refresh the conversation.");
                terminal = true;
                return;
            }
            onDone(payload as StreamDoneEvent);
            terminal = true;
        } else if (eventName === "error") {
            onError(payload.error ?? "The AI request failed.");
            terminal = true;
        }
    };

    while (true) {
        const { value, done } = await reader.read();
        buffer += decoder.decode(value ?? new Uint8Array(), { stream: !done });

        let separator = /\r?\n\r?\n/.exec(buffer);
        while (separator) {
            processEvent(buffer.slice(0, separator.index));
            buffer = buffer.slice(separator.index + separator[0].length);
            if (terminal) {
                // Completion must not wait for network cleanup to enable the next turn.
                void reader.cancel().catch(() => {});
                reader.releaseLock();
                return;
            }
            separator = /\r?\n\r?\n/.exec(buffer);
        }

        if (done) break;
    }

    if (buffer.trim()) {
        processEvent(buffer);
    }
    reader.releaseLock();
}

export async function streamAIMessage(
    conversationId: string,
    message: string,
    onToken: (text: string) => void
) : Promise<StreamDoneEvent> {
    const result: { doneEvent: StreamDoneEvent | null; error: string | null } = {
        doneEvent: null,
        error: null
    };

    await fetchStream(
        conversationId,
        message,
        onToken,
        event => {
            result.doneEvent = event;
        },
        error => {
            result.error = error;
        }
    );

    if (result.error) {
        throw new Error(result.error);
    }

    if (!result.doneEvent) {
        throw new Error("The AI stream ended before a completion was received.");
    }

    return result.doneEvent;
}

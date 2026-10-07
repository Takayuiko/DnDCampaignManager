import { extractApiResponseError, extractErrorMessage } from "../Utils/apiError";
import api, { refreshAccessToken } from "./axios";
import { waitWithSignal } from "../Utils/requestCancellation";

export type ConversationSummary = {
    id: string;
    title: string;
    createdAtUtc: string;
    updatedAtUtc: string;
    messageCount: number;
    campaignId?: number | null;
};

export type KnowledgeSource = {
    mapId?: number | null;
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

export async function getAIStatus(signal?: AbortSignal) {
    return api.get<AIStatus>("/ai/status", { signal });
}

export async function getConversations(signal?: AbortSignal) {
    return api.get<ConversationSummary[]>("/ai/conversations", { signal });
}

export async function createConversation(title?: string, campaignId?: number, signal?: AbortSignal) {
    return api.post<ConversationSummary>("/ai/conversations", {
        title: title ?? null,
        campaignId: campaignId ?? null
    }, { signal });
}

export async function getDefaultConversation(signal?: AbortSignal) {
    return api.post<ConversationSummary | undefined>("/ai/conversations/default", undefined, { signal });
}

export async function getConversation(conversationId: string, signal?: AbortSignal) {
    return api.get<Conversation>(`/ai/conversations/${conversationId}`, { signal });
}

export async function deleteConversation(conversationId: string, signal?: AbortSignal) {
    return api.delete<ConversationSummary | undefined>(`/ai/conversations/${conversationId}`, { signal });
}

async function fetchStream(
    conversationId: string,
    message: string,
    onToken: (text: string) => void,
    onDone: (event: StreamDoneEvent) => void,
    onError: (message: string) => void,
    signal: AbortSignal,
    retryAfterRefresh = true
) {
    const baseURL = (api.defaults.baseURL ?? "/api").replace(/\/$/, "");
    const url = `${baseURL}/ai/conversations/${conversationId}/messages/stream`;
    const token = localStorage.getItem("token");

    signal.throwIfAborted();
    const response = await waitWithSignal(fetch(url, {
        method: "POST",
        signal,
        headers: {
            "Content-Type": "application/json",
            ...(token ? { Authorization: `Bearer ${token}` } : {})
        },
        body: JSON.stringify({ message })
    }), signal);

    if (response.status === 401 && retryAfterRefresh) {
        try {
            void response.body?.cancel().catch(() => {});
            await waitWithSignal(refreshAccessToken(token), signal);
            signal.throwIfAborted();
            return fetchStream(
                conversationId,
                message,
                onToken,
                onDone,
                onError,
                signal,
                false
            );
        } catch {
            signal.throwIfAborted();
            throw new Error("Your session has expired.");
        }
    }

    if (!response.ok || !response.body) {
        let messageText = "The AI request failed.";
        try {
            const body = await waitWithSignal(response.json(), signal);
            messageText = extractApiResponseError(response.status, body, messageText);
        } catch {
            signal.throwIfAborted();
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
            onError(extractErrorMessage(payload, "The AI request failed."));
            terminal = true;
        }
    };

    try {
        while (true) {
            const { value, done } = await waitWithSignal(reader.read(), signal);
            buffer += decoder.decode(value ?? new Uint8Array(), { stream: !done });

            let separator = /\r?\n\r?\n/.exec(buffer);
            while (separator) {
                signal.throwIfAborted();
                processEvent(buffer.slice(0, separator.index));
                buffer = buffer.slice(separator.index + separator[0].length);
                if (terminal) {
                    // Completion must not wait for network cleanup to enable the next turn.
                    return;
                }
                separator = /\r?\n\r?\n/.exec(buffer);
            }

            if (done) break;
        }

        if (buffer.trim()) {
            signal.throwIfAborted();
            processEvent(buffer);
        }
    } finally {
        void reader.cancel().catch(() => {});
        reader.releaseLock();
    }
}

export async function streamAIMessage(
    conversationId: string,
    message: string,
    onToken: (text: string) => void,
    signal?: AbortSignal
) : Promise<StreamDoneEvent> {
    const result: { doneEvent: StreamDoneEvent | null; error: string | null } = {
        doneEvent: null,
        error: null
    };

    const controller = new AbortController();
    const cancel = () => controller.abort(signal?.reason);
    if (signal?.aborted) cancel();
    else signal?.addEventListener("abort", cancel, { once: true });
    const timer = setTimeout(() => controller.abort(new DOMException(
        "The AI request timed out. Reload the conversation before sending again.", "TimeoutError")), 150000);
    try {
        await fetchStream(
            conversationId,
            message,
            onToken,
            event => {
                result.doneEvent = event;
            },
            error => {
                result.error = error;
            },
            controller.signal
        );
    } finally {
        clearTimeout(timer);
        signal?.removeEventListener("abort", cancel);
    }

    if (result.error) {
        throw new Error(result.error);
    }

    if (!result.doneEvent) {
        throw new Error("The AI stream ended before a completion was received.");
    }

    return result.doneEvent;
}

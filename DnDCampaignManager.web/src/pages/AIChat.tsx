import { type FormEvent, useEffect, useMemo, useState } from "react";
import {
    createConversation,
    deleteConversation,
    getAIStatus,
    getConversation,
    getConversations,
    streamAIMessage,
    type AIStatus,
    type Conversation,
    type ConversationSummary,
    type ConversationMessage
} from "../api/aiApi";

function formatCost(value: number) {
    return value === 0 ? "$0.00" : `$${value.toFixed(6)}`;
}

function MessageBubble({ message }: { message: ConversationMessage }) {
    const isUser = message.role === "user";

    return (
        <div
            className={[
                "max-w-[88%] rounded-2xl px-4 py-3 whitespace-pre-wrap",
                isUser
                    ? "ml-auto bg-stone-800 text-white"
                    : "mr-auto bg-stone-100 text-stone-800"
            ].join(" ")}
        >
            <div className="mb-1 text-xs font-semibold opacity-70">
                {isUser ? "You" : "Dungeon Master AI"}
            </div>
            {message.content}

            {!isUser && (
                <div className="mt-3 border-t border-stone-200 pt-2 text-[11px] text-stone-500">
                    {message.model ?? "AI"} · {message.totalTokenCount.toLocaleString()} tokens · {formatCost(message.estimatedCostUsd)} · {message.durationMs} ms
                </div>
            )}
        </div>
    );
}

export default function AIChat() {
    const [conversations, setConversations] = useState<ConversationSummary[]>([]);
    const [conversation, setConversation] = useState<Conversation | null>(null);
    const [status, setStatus] = useState<AIStatus | null>(null);
    const [input, setInput] = useState("");
    const [loading, setLoading] = useState(false);
    const [loadingConversation, setLoadingConversation] = useState(false);
    const [error, setError] = useState("");

    const currentMessages = conversation?.messages ?? [];

    const totalCost = useMemo(
        () => currentMessages.reduce((sum, message) => sum + message.estimatedCostUsd, 0),
        [currentMessages]
    );

    const totalTokens = useMemo(
        () => currentMessages.reduce((sum, message) => sum + message.totalTokenCount, 0),
        [currentMessages]
    );

    useEffect(() => {
        void initialize();
    }, []);

    async function initialize() {
        try {
            const [statusResponse, conversationsResponse] = await Promise.all([
                getAIStatus(),
                getConversations()
            ]);

            setStatus(statusResponse.data);
            const items = conversationsResponse.data;
            setConversations(items);

            if (items.length > 0) {
                await openConversation(items[0].id);
            } else {
                await newConversation();
            }
        } catch {
            setError("Unable to load the AI assistant.");
        }
    }

    async function openConversation(id: string) {
        setLoadingConversation(true);
        setError("");

        try {
            const response = await getConversation(id);
            setConversation(response.data);
        } catch {
            setError("Unable to load this conversation.");
        } finally {
            setLoadingConversation(false);
        }
    }

    async function newConversation() {
        try {
            const response = await createConversation();
            const created = response.data;
            setConversations(current => [created, ...current]);
            setConversation({
                ...created,
                messages: []
            });
            setInput("");
            setError("");
        } catch {
            setError("Unable to create a conversation.");
        }
    }

    async function removeConversation(id: string) {
        try {
            await deleteConversation(id);
            const remaining = conversations.filter(item => item.id !== id);
            setConversations(remaining);

            if (conversation?.id === id) {
                if (remaining.length > 0) {
                    await openConversation(remaining[0].id);
                } else {
                    await newConversation();
                }
            }
        } catch {
            setError("Unable to delete this conversation.");
        }
    }

    async function sendMessage(event: FormEvent) {
        event.preventDefault();

        const message = input.trim();
        if (!message || loading || !conversation) return;

        setError("");
        setInput("");
        setLoading(true);

        const userMessage: ConversationMessage = {
            id: -Date.now(),
            role: "user",
            content: message,
            createdAtUtc: new Date().toISOString(),
            inputTokenCount: 0,
            outputTokenCount: 0,
            totalTokenCount: 0,
            estimatedCostUsd: 0,
            durationMs: 0,
            status: "completed"
        };

        const assistantMessage: ConversationMessage = {
            id: -Date.now() - 1,
            role: "assistant",
            content: "",
            createdAtUtc: new Date().toISOString(),
            inputTokenCount: 0,
            outputTokenCount: 0,
            totalTokenCount: 0,
            estimatedCostUsd: 0,
            durationMs: 0,
            status: "streaming",
            model: status?.model
        };

        setConversation(current => current
            ? { ...current, messages: [...current.messages, userMessage, assistantMessage] }
            : current
        );

        try {
            const done = await streamAIMessage(
                conversation.id,
                message,
                token => {
                    setConversation(current => {
                        if (!current) return current;

                        const messages = [...current.messages];
                        const index = messages.findIndex(item => item.id === assistantMessage.id);
                        if (index >= 0) {
                            messages[index] = {
                                ...messages[index],
                                content: messages[index].content + token
                            };
                        }
                        return { ...current, messages };
                    });
                }
            );

            setConversation(current => {
                if (!current) return current;

                return {
                    ...current,
                    updatedAtUtc: new Date().toISOString(),
                    messages: current.messages.map(item =>
                        item.id === assistantMessage.id
                            ? {
                                ...item,
                                id: done.messageId,
                                model: done.model,
                                inputTokenCount: done.usage.inputTokens,
                                outputTokenCount: done.usage.outputTokens,
                                totalTokenCount: done.usage.totalTokens,
                                estimatedCostUsd: done.usage.estimatedCostUsd,
                                status: "completed"
                            }
                            : item
                    )
                };
            });

            setConversations(current => current.map(item =>
                item.id === conversation.id
                    ? {
                        ...item,
                        updatedAtUtc: new Date().toISOString(),
                        messageCount: item.messageCount + 2,
                        title: item.title === "New AI Conversation"
                            ? message.length > 80 ? `${message.slice(0, 80)}...` : message
                            : item.title
                    }
                    : item
            ));
        } catch (requestError) {
            const errorMessage = requestError instanceof Error
                ? requestError.message
                : "The AI request failed.";

            setError(errorMessage);
            setConversation(current => current
                ? {
                    ...current,
                    messages: current.messages.filter(item => item.id !== assistantMessage.id)
                }
                : current
            );
        } finally {
            setLoading(false);
        }
    }

    return (
        <div className="min-h-screen bg-gradient-to-b from-stone-100 to-amber-50 px-4 py-6">
            <div className="mx-auto max-w-7xl">
                <div className="mb-5 flex flex-wrap items-end justify-between gap-4">
                    <div>
                        <h1 className="text-3xl font-bold text-stone-800">🤖 AI Dungeon Master</h1>
                        <p className="mt-1 text-sm text-stone-600">
                            Phase 2: persistent conversations, streaming responses and usage tracking.
                        </p>
                    </div>

                    <div className="flex items-center gap-3 text-xs text-stone-600">
                        <span>{totalTokens.toLocaleString()} tokens in this conversation</span>
                        <span>·</span>
                        <span>{formatCost(totalCost)}</span>
                    </div>
                </div>

                {error && (
                    <div className="mb-4 rounded-lg border border-red-200 bg-red-50 p-3 text-sm text-red-800">
                        {error}
                    </div>
                )}

                <div className="grid min-h-[650px] gap-4 lg:grid-cols-[280px_1fr]">
                    <aside className="rounded-2xl border border-stone-300 bg-white p-3 shadow-sm">
                        <div className="mb-3 flex items-center justify-between">
                            <h2 className="font-bold text-stone-800">Conversations</h2>
                            <button
                                onClick={() => void newConversation()}
                                className="rounded-lg bg-stone-800 px-3 py-1.5 text-xs font-semibold text-white hover:bg-stone-700"
                            >
                                + New
                            </button>
                        </div>

                        <div className="space-y-2 overflow-y-auto lg:max-h-[580px]">
                            {conversations.map(item => (
                                <div
                                    key={item.id}
                                    className={[
                                        "group rounded-xl border p-3 transition",
                                        conversation?.id === item.id
                                            ? "border-amber-400 bg-amber-50"
                                            : "border-stone-200 hover:bg-stone-50"
                                    ].join(" ")}
                                >
                                    <button
                                        onClick={() => void openConversation(item.id)}
                                        className="w-full text-left"
                                    >
                                        <div className="truncate text-sm font-semibold text-stone-800">
                                            {item.title}
                                        </div>
                                        <div className="mt-1 text-xs text-stone-500">
                                            {item.messageCount} messages
                                        </div>
                                    </button>
                                    <button
                                        onClick={() => void removeConversation(item.id)}
                                        className="mt-2 text-xs text-red-700 opacity-70 hover:opacity-100"
                                    >
                                        Delete
                                    </button>
                                </div>
                            ))}
                        </div>
                    </aside>

                    <section className="flex min-h-[650px] flex-col rounded-2xl border border-stone-300 bg-white shadow-lg">
                        <div className="flex items-center justify-between border-b border-stone-200 px-5 py-4">
                            <div>
                                <div className="font-bold text-stone-800">
                                    {conversation?.title ?? "Conversation"}
                                </div>
                                <div className="text-xs text-stone-500">
                                    {status?.provider ?? "AI"} · {status?.model ?? "—"} · {status?.configured ? "Configured" : "Not configured"}
                                </div>
                            </div>
                            {loadingConversation && (
                                <span className="text-xs text-stone-500">Loading...</span>
                            )}
                        </div>

                        <div className="flex-1 space-y-4 overflow-y-auto p-5">
                            {currentMessages.length === 0 && (
                                <div className="rounded-xl bg-amber-50 p-5 text-stone-700">
                                    <p className="font-semibold">Start a conversation.</p>
                                    <p className="mt-1 text-sm">
                                        Your conversation will now be saved to PostgreSQL, and responses will stream into the chat as they are generated.
                                    </p>
                                </div>
                            )}

                            {currentMessages.map(message => (
                                <MessageBubble key={message.id} message={message} />
                            ))}
                        </div>

                        <form onSubmit={sendMessage} className="border-t border-stone-200 p-4">
                            <div className="flex gap-3">
                                <input
                                    value={input}
                                    onChange={event => setInput(event.target.value)}
                                    placeholder="Ask the Dungeon Master..."
                                    disabled={loading || !conversation}
                                    className="min-w-0 flex-1 rounded-xl border border-stone-300 bg-white px-4 py-3 text-stone-800 outline-none focus:border-stone-500 focus:ring-2 focus:ring-amber-200 disabled:bg-stone-100"
                                />
                                <button
                                    type="submit"
                                    disabled={loading || !input.trim() || !conversation}
                                    className="rounded-xl bg-stone-800 px-5 py-3 font-semibold text-white shadow hover:bg-stone-700 disabled:cursor-not-allowed disabled:opacity-50"
                                >
                                    {loading ? "Thinking..." : "Send"}
                                </button>
                            </div>
                        </form>
                    </section>
                </div>
            </div>
        </div>
    );
}

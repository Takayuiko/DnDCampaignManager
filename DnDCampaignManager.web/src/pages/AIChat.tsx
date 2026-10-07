import { type FormEvent, useEffect, useMemo, useRef, useState } from "react";
import {
    createConversation,
    deleteConversation,
    getAIStatus,
    getConversation,
    getConversations,
    getDefaultConversation,
    streamAIMessage,
    type AIStatus,
    type Conversation,
    type ConversationSummary,
    type ConversationMessage
} from "../api/aiApi";
import { getCampaigns, type CampaignSummary } from "../api/campaignApi";


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

            {!isUser && message.retrievalWarning && (
                <p className="mt-3 text-xs text-amber-800">{message.retrievalWarning}</p>
            )}
            {!isUser && !!message.sources?.length && (
                <details className="mt-3 whitespace-normal text-xs">
                    <summary className="cursor-pointer font-semibold">Retrieved campaign references ({message.sources.length})</summary>
                    {message.sources.map(source => (
                        <div key={source.label} className="mt-2 rounded border border-stone-200 bg-white p-2">
                            <p className="font-semibold">[{source.label}] {source.mapId ? "Map" : `Session ${source.sessionNumber}`}: {source.title}</p>
                            <p className="mt-1 whitespace-pre-wrap">{source.excerpt}</p>
                        </div>
                    ))}
                </details>
            )}

            {!isUser && (
                <div className="mt-3 border-t border-stone-200 pt-2 text-[11px] text-stone-500">
                    {message.status === "streaming"
                        ? "Generating… usage will appear when the reply finishes."
                        : message.status === "completed"
                            ? `${message.model ?? "AI"} · ${message.inputTokenCount.toLocaleString()} input + ${message.outputTokenCount.toLocaleString()} output = ${message.totalTokenCount.toLocaleString()} tokens · ${formatCost(message.estimatedCostUsd)} · ${message.durationMs} ms`
                            : "Reply interrupted. Usage is unavailable."}
                </div>
            )}
            {!isUser && message.status === "completed" && (message.retrievalInputTokens ?? 0) > 0 && (
                <div className="mt-1 text-[11px] text-stone-500">
                    Session search: {message.retrievalInputTokens?.toLocaleString()} embedding input tokens (billed separately).
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
    const [loadingConversation, setLoadingConversation] = useState(true);
    const [error, setError] = useState("");
    const [initializationAttempt, setInitializationAttempt] = useState(0);
    const [campaigns, setCampaigns] = useState<CampaignSummary[]>([]);
    const [campaignError, setCampaignError] = useState("");
    const initialization = useRef<Promise<{ items: ConversationSummary[]; active: Conversation | null }> | null>(null);
    const selectedCampaignId = String(conversation?.campaignId ?? "");
    const selectionRequest = useRef(0);

    const currentMessages = useMemo(() => conversation?.messages ?? [], [conversation?.messages]);
    const messageList = useRef<HTMLDivElement>(null);
    const followMessages = useRef(true);

    useEffect(() => { followMessages.current = true; }, [conversation?.id]);
    useEffect(() => {
        if (followMessages.current && messageList.current) {
            messageList.current.scrollTop = messageList.current.scrollHeight;
        }
    }, [currentMessages]);

    const totalCost = useMemo(
        () => currentMessages.reduce((sum, message) => sum + message.estimatedCostUsd, 0),
        [currentMessages]
    );

    const totalTokens = useMemo(
        () => currentMessages.reduce((sum, message) => sum + message.totalTokenCount, 0),
        [currentMessages]
    );

    useEffect(() => {
        let active = true;
        void getCampaigns().then(response => { if (active) setCampaigns(response.data); }).catch(() => {
            if (active) setCampaignError("Unable to load campaigns. Reload to try again.");
        });
        return () => { active = false; };
    }, []);

    useEffect(() => {
        let active = true;
        void getAIStatus().then(response => { if (active) setStatus(response.data); }).catch(() => {});
        initialization.current ??= (async () => {
            const defaultResponse = await getDefaultConversation();
            const [list, selected] = await Promise.all([
                getConversations(), defaultResponse.data ? getConversation(defaultResponse.data.id) : Promise.resolve(null)
            ]);
            return { items: list.data, active: selected?.data ?? null };
        })();
        void initialization.current.then(result => {
            if (!active) return;
            setConversations(result.items);
            setConversation(result.active);
            setError("");
        }).catch(() => {
            if (active) setError("Unable to load your conversation. Please retry.");
        }).finally(() => {
            if (active) setLoadingConversation(false);
        });
        return () => { active = false; };
    }, [initializationAttempt]);

    async function selectCampaign(campaignId: number) {
        const requestId = ++selectionRequest.current;
        setLoadingConversation(true);
        setConversation(null);
        setInput("");
        setError("");
        try {
            const response = await createConversation(undefined, campaignId);
            const selected = await getConversation(response.data.id);
            if (requestId !== selectionRequest.current) return;
            if (selected.data.campaignId !== campaignId) throw new Error("Campaign mismatch");
            setConversation(selected.data);
            setConversations(current => [...current.filter(item => item.campaignId !== campaignId), response.data]);
        } catch {
            if (requestId === selectionRequest.current) setError("Unable to load this campaign chat. Select a campaign to retry.");
        } finally {
            if (requestId === selectionRequest.current) setLoadingConversation(false);
        }
    }

    async function clearConversation() {
        if (!conversation || loading || loadingConversation) return;
        setLoadingConversation(true);
        try {
            const response = await deleteConversation(conversation.id);
            if (!response.data) throw new Error("Missing campaign chat");
            const replacement = response.data;
            setConversations(current => current.map(item => item.id === replacement.id ? replacement : item));
            setConversation({ ...replacement, messages: [] });
            setInput("");
            setError("");
        } catch {
            setError("Unable to clear this campaign chat.");
        } finally {
            setLoadingConversation(false);
        }
    }

    async function sendMessage(event: FormEvent) {
        event.preventDefault();

        const message = input.trim();
        if (loading || loadingConversation || !conversation) return;
        if (!message) {
            setError("Enter a message before sending.");
            return;
        }
        followMessages.current = true;

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
                        if (!current || current.id !== conversation.id) return current;

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
                if (!current || current.id !== conversation.id) return current;

                return {
                    ...current,
                    updatedAtUtc: new Date().toISOString(),
                    messages: current.messages.map(item =>
                        item.id === assistantMessage.id
                            ? {
                                ...item,
                                id: done.messageId,
                                model: done.model,
                                content: done.reply,
                                durationMs: done.durationMs,
                                sources: done.sources,
                                retrievalInputTokens: done.retrievalInputTokens,
                                retrievalWarning: done.retrievalWarning,
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
                    messages: current.messages.flatMap(item => item.id === assistantMessage.id
                        ? item.content ? [{ ...item, status: "failed" }] : []
                        : [item])
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
                            Ask about your campaign, characters and saved session history.
                        </p>
                    </div>

                    <div className="flex items-center gap-3 text-xs text-stone-600">
                        <span>{totalTokens.toLocaleString()} tokens used (input + output)</span>
                        <span>·</span>
                        <span>{formatCost(totalCost)}</span>
                    </div>
                </div>

                {error && (
                    <div className="mb-4 rounded-lg border border-red-200 bg-red-50 p-3 text-sm text-red-800">
                        {error}
                        {!conversation && !loadingConversation && (
                            <button type="button" className="ml-3 underline" onClick={() => {
                                initialization.current = null;
                                setLoadingConversation(true);
                                setInitializationAttempt(current => current + 1);
                            }}>Retry</button>
                        )}
                    </div>
                )}

                <div className="grid min-h-[650px] gap-4 lg:grid-cols-[280px_1fr]">
                    <aside className="rounded-2xl border border-stone-300 bg-white p-3 shadow-sm">
                        <label className="mb-1 block text-xs font-semibold text-stone-700" htmlFor="chat-campaign">Campaign chat</label>
                        <select id="chat-campaign" value={selectedCampaignId}
                            onChange={event => { if (event.target.value) void selectCampaign(Number(event.target.value)); }} disabled={loading || loadingConversation}
                            className="mb-3 w-full rounded border border-stone-300 p-2 text-sm">
                            <option value="" disabled>{campaigns.length === 0 ? "No campaigns available" : "Select a campaign"}</option>
                            {campaigns.map(campaign => <option key={campaign.id} value={campaign.id}>{campaign.name}</option>)}
                        </select>
                        {campaignError && <p className="mb-3 text-xs text-red-700">{campaignError}</p>}
                        <p className="mb-3 text-xs text-stone-600">One personal chat per campaign.</p>
                        {conversation && <>
                            <p className="mb-3 text-sm text-stone-600">{conversations.find(item => item.id === conversation.id)?.messageCount ?? conversation.messages.length} messages</p>
                            <button onClick={() => void clearConversation()} disabled={loading || loadingConversation}
                                className="text-xs text-red-700 hover:underline">Clear chat</button>
                        </>}
                    </aside>

                    <section className="flex h-[650px] min-h-0 flex-col rounded-2xl border border-stone-300 bg-white shadow-lg">
                        <div className="flex items-center justify-between border-b border-stone-200 px-5 py-4">
                            <div>
                                <div className="font-bold text-stone-800">
                                    {conversation?.title ?? "Conversation"}
                                </div>
                                <div className="text-xs text-stone-500">
                                    {status?.provider ?? "AI"} · {status?.model ?? "—"} · {status?.configured ? "Configured" : "Not configured"}
                                </div>
                                <div className="mt-1 text-xs text-stone-600">
                                    {conversation?.campaignId
                                        ? <a className="underline" href={`/campaigns/${conversation.campaignId}/session-notes`}>
                                            {campaigns.find(c => c.id === conversation.campaignId)?.name ?? "Campaign"} · Session notes
                                        </a>
                                        : "Select a campaign to open its chat."}
                                </div>
                            </div>
                            {loadingConversation && (
                                <span className="text-xs text-stone-500">Loading...</span>
                            )}
                        </div>

                        <div ref={messageList}
                            onScroll={event => {
                                const list = event.currentTarget;
                                followMessages.current = list.scrollHeight - list.scrollTop - list.clientHeight < 80;
                            }}
                            className="min-h-0 flex-1 space-y-4 overflow-y-auto p-5">
                            {currentMessages.length === 0 && (
                                <div className="rounded-xl bg-amber-50 p-5 text-stone-700">
                                    <p className="font-semibold">{!conversation && !loadingConversation ? "Join a campaign to use AI chat." : "Start a conversation."}</p>
                                    <p className="mt-1 text-sm">
                                        {conversation?.campaignId
                                            ? "Ask about current characters or past events. Relevant saved session notes will appear with the reply."
                                            : !conversation && !loadingConversation
                                                ? "Ask your DM to invite you to a campaign, then reload this page."
                                                : "Choose a campaign to ask about its characters and session history."}
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
                                    disabled={loading || loadingConversation || !conversation}
                                    className="min-w-0 flex-1 rounded-xl border border-stone-300 bg-white px-4 py-3 text-stone-800 outline-none focus:border-stone-500 focus:ring-2 focus:ring-amber-200 disabled:bg-stone-100"
                                />
                                <button
                                    type="submit"
                                    disabled={loading || loadingConversation || !conversation}
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

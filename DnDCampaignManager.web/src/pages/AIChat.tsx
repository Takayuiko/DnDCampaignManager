import { FormEvent, useEffect, useState } from "react";
import {
    getAIStatus,
    sendAIMessage,
    type AIStatus,
    type ChatMessage
} from "../api/aiApi";

export default function AIChat() {
    const [messages, setMessages] = useState<ChatMessage[]>([]);
    const [input, setInput] = useState("");
    const [status, setStatus] = useState<AIStatus | null>(null);
    const [loading, setLoading] = useState(false);
    const [error, setError] = useState("");

    useEffect(() => {
        getAIStatus()
            .then(response => setStatus(response.data))
            .catch(() => setError("Unable to check the AI service."));
    }, []);

    const sendMessage = async (event: FormEvent) => {
        event.preventDefault();

        const message = input.trim();
        if (!message || loading) return;

        setError("");

        const nextMessages: ChatMessage[] = [
            ...messages,
            { role: "user", content: message }
        ];

        setMessages(nextMessages);
        setInput("");
        setLoading(true);

        try {
            const response = await sendAIMessage(message, messages);

            setMessages([
                ...nextMessages,
                {
                    role: "assistant",
                    content: response.data.reply
                }
            ]);
        } catch {
            setError("The AI request failed. Check the API logs and configuration.");
        } finally {
            setLoading(false);
        }
    };

    return (
        <div className="min-h-screen bg-gradient-to-b from-stone-100 to-amber-50 px-4 py-8">
            <div className="mx-auto max-w-4xl">
                <div className="mb-6">
                    <h1 className="text-3xl font-bold text-stone-800">
                        🤖 AI Dungeon Master
                    </h1>
                    <p className="mt-1 text-sm text-stone-600">
                        Phase 1: direct LLM integration. Campaign knowledge and tools
                        will be added in later phases.
                    </p>
                </div>

                <div className="mb-4 rounded-xl border border-stone-300 bg-white p-4 shadow-sm">
                    <div className="flex flex-wrap items-center gap-3 text-sm">
                        <span className="font-semibold text-stone-700">
                            Provider:
                        </span>
                        <span>{status?.provider ?? "Checking..."}</span>

                        <span className="font-semibold text-stone-700">
                            Model:
                        </span>
                        <span>{status?.model ?? "—"}</span>

                        <span
                            className={[
                                "rounded-full px-2 py-1 text-xs font-semibold",
                                status?.configured
                                    ? "bg-green-100 text-green-800"
                                    : "bg-red-100 text-red-800"
                            ].join(" ")}
                        >
                            {status?.configured ? "Configured" : "Not configured"}
                        </span>
                    </div>
                </div>

                <div className="flex min-h-[500px] flex-col rounded-2xl border border-stone-300 bg-white shadow-lg">
                    <div className="flex-1 space-y-4 overflow-y-auto p-5">
                        {messages.length === 0 && (
                            <div className="rounded-xl bg-amber-50 p-5 text-stone-700">
                                <p className="font-semibold">Start a conversation.</p>
                                <p className="mt-1 text-sm">
                                    Try asking something like:
                                    <br />
                                    <span className="italic">
                                        "Give me an idea for an ancient temple encounter."
                                    </span>
                                </p>
                            </div>
                        )}

                        {messages.map((message, index) => (
                            <div
                                key={`${message.role}-${index}`}
                                className={[
                                    "max-w-[85%] rounded-2xl px-4 py-3 whitespace-pre-wrap",
                                    message.role === "user"
                                        ? "ml-auto bg-stone-800 text-white"
                                        : "mr-auto bg-stone-100 text-stone-800"
                                ].join(" ")}
                            >
                                <div className="mb-1 text-xs font-semibold opacity-70">
                                    {message.role === "user" ? "You" : "Dungeon Master AI"}
                                </div>
                                {message.content}
                            </div>
                        ))}

                        {loading && (
                            <div className="mr-auto max-w-[85%] rounded-2xl bg-stone-100 px-4 py-3 text-stone-500">
                                Thinking...
                            </div>
                        )}
                    </div>

                    {error && (
                        <div className="mx-5 mb-3 rounded-lg border border-red-200 bg-red-50 p-3 text-sm text-red-800">
                            {error}
                        </div>
                    )}

                    <form
                        onSubmit={sendMessage}
                        className="border-t border-stone-200 p-4"
                    >
                        <div className="flex gap-3">
                            <input
                                value={input}
                                onChange={event => setInput(event.target.value)}
                                placeholder="Ask the Dungeon Master..."
                                disabled={loading}
                                className="min-w-0 flex-1 rounded-xl border border-stone-300 bg-white px-4 py-3 text-stone-800 outline-none focus:border-stone-500 focus:ring-2 focus:ring-amber-200 disabled:bg-stone-100"
                            />
                            <button
                                type="submit"
                                disabled={loading || !input.trim()}
                                className="rounded-xl bg-stone-800 px-5 py-3 font-semibold text-white shadow hover:bg-stone-700 disabled:cursor-not-allowed disabled:opacity-50"
                            >
                                Send
                            </button>
                        </div>
                    </form>
                </div>
            </div>
        </div>
    );
}

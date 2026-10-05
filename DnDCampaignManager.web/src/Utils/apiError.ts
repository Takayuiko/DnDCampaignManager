import { isAxiosError } from "axios";

function safeMessage(value: unknown): value is string {
    return typeof value === "string" && value.trim().length > 0 && value.length <= 300 &&
        !/[\r\n<>]|\bSystem\.|\bException\b|\bat\s+\S+\(/i.test(value);
}

export function extractApiError(err: unknown, fallback: string): string {
    if (!isAxiosError(err) || !err.response) return fallback;
    const { status, data } = err.response;
    // Only expected client errors may supply UI messages. Never render server diagnostics.
    if (![400, 404, 409, 422].includes(status)) return fallback;
    if (safeMessage(data)) return data.trim();
    if (data && typeof data === "object" && "errors" in data &&
        data.errors && typeof data.errors === "object") {
        const messages = Object.values(data.errors).flat().filter(safeMessage);
        if (messages.length) return messages.join(" ").slice(0, 600);
    }
    return fallback;
}

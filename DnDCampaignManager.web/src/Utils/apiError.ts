import { isAxiosError } from "axios";

function safeMessage(value: unknown): value is string {
    return typeof value === "string" && value.trim().length > 0 && value.length <= 300 &&
        !/[\r\n<>]|\bSystem\.|\bException\b|\bat\s+\S+\(/i.test(value);
}

export function extractApiError(err: unknown, fallback: string): string {
    if (!isAxiosError(err) || !err.response) return fallback;
    return extractApiResponseError(err.response.status, err.response.data, fallback);
}

export function extractLoginError(err: unknown): string {
    if (isAxiosError(err) && err.response?.status === 401) return "Invalid email or password.";
    if (isAxiosError(err) && err.response?.status === 429) return "Too many attempts. Please try again later.";
    return extractApiError(err, "Login failed. Please try again.");
}

export function extractApiResponseError(status: number, data: unknown, fallback: string): string {
    // Only expected client errors may supply UI messages. Never render server diagnostics.
    if (![400, 404, 409, 422].includes(status)) return fallback;
    return extractErrorMessage(data, fallback);
}

export function extractErrorMessage(data: unknown, fallback: string): string {
    if (safeMessage(data)) return data.trim();
    if (data && typeof data === "object") {
        for (const key of ["message", "error", "detail"] as const) {
            if (key in data) {
                const value = (data as Record<string, unknown>)[key];
                if (safeMessage(value)) return value.trim();
            }
        }
    }
    if (data && typeof data === "object" && "errors" in data &&
        data.errors && typeof data.errors === "object") {
        const messages = Object.values(data.errors).flat().filter(safeMessage);
        if (messages.length) return messages.join(" ").slice(0, 600);
    }
    return fallback;
}

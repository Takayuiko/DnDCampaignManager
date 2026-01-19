export function extractApiError(err: any, fallback: string) {
    const data = err?.response?.data;

    // plain string message from API
    if (typeof data === "string") return data;

    // ASP.NET validation errors shape: { errors: { Field: [msg] } }
    if (data?.errors && typeof data.errors === "object") {
        const parts: string[] = [];
        for (const key of Object.keys(data.errors)) {
            const msgs = data.errors[key];
            if (Array.isArray(msgs)) parts.push(...msgs);
        }
        if (parts.length) return parts.join(" ");
    }

    // last resort
    return fallback;
}

import axios from "axios";
import { waitWithSignal } from "../Utils/requestCancellation";

const baseURL =
    import.meta.env.VITE_API_BASE_URL?.replace(/\/$/, "") ?? "/api";
// - local dev: set VITE_API_BASE_URL=http://localhost:5000/api
// - production: you can use "/api" if you host SPA + API together,
//   or set it to https://your-api.azurewebsites.net/api

const api = axios.create({
    baseURL,
    withCredentials: true,
    timeout: 30000,
});

let refreshPromise: Promise<string> | null = null;
let loggingOut = false;

export const setLoggingOut = (value: boolean) => {
    loggingOut = value;
};

// Axios requests and fetch-based streams share the same refresh operation.
export function refreshAccessToken(failedToken: string | null = localStorage.getItem("token")): Promise<string> {
    if (!refreshPromise) {
        const refresh = async () => {
            const currentToken = localStorage.getItem("token");
            if (!currentToken || loggingOut) throw new Error("Your session has expired.");
            // A queued tab (or a late 401) can reuse the token published by the winner.
            if (currentToken !== failedToken) return currentToken;
            const { data } = await api.post<{ accessToken: string }>("/auth/refresh", undefined, { timeout: 15000 });
            if (localStorage.getItem("token") !== currentToken || loggingOut)
                throw new Error("The session changed during refresh.");
            if (!data.accessToken) throw new Error("Invalid refresh response.");
            localStorage.setItem("token", data.accessToken);
            api.defaults.headers.common.Authorization = `Bearer ${data.accessToken}`;
            return data.accessToken;
        };
        const fail = (error: unknown): never => {
            // Publish failure before releasing the lock so queued tabs cannot rotate again.
            // Never clear a newer session established while this request was waiting.
            if (localStorage.getItem("token") === failedToken) localStorage.removeItem("token");
            if (!localStorage.getItem("token")) {
                delete api.defaults.headers.common.Authorization;
                if (!loggingOut) window.location.href = "/login";
            }
            throw error;
        };
        // Do not fall back to racing cookie rotations on unsupported/insecure origins.
        const locks = typeof navigator !== "undefined" ? navigator.locks : undefined;
        refreshPromise = (locks
            ? locks.request("dnd-auth-refresh", () => refresh().catch(fail)).then(token => token)
            : Promise.reject(new Error("Automatic refresh requires a browser with Web Locks on HTTPS or localhost.")))
            .catch(fail)
            .finally(() => {
                refreshPromise = null;
            });
    }
    return refreshPromise;
}

api.interceptors.request.use(config => {
    const token = localStorage.getItem("token");
    if (token) {
        config.headers.Authorization = `Bearer ${token}`;
    } else delete config.headers.Authorization;
    return config;
});

api.interceptors.response.use(
    res => res,
    async error => {
        const original = error.config;

        if (loggingOut || original?.signal?.aborted || error.code === "ERR_CANCELED") return Promise.reject(error);

        const isAuthEndpoint =
            original?.url?.includes("/auth/login") ||
            original?.url?.includes("/auth/register") ||
            original?.url?.includes("/auth/refresh");

        // Don't try to refresh if login/register/refresh itself fails
        if (error.response?.status === 401 && isAuthEndpoint) {
            return Promise.reject(error);
        }

        if (error.response?.status === 401 && original && !original._retry) {
            original._retry = true;
            try {
                const authorization = original.headers?.Authorization;
                const failedToken = typeof authorization === "string" ? authorization.replace(/^Bearer /, "") : localStorage.getItem("token");
                const newToken = await waitWithSignal(refreshAccessToken(failedToken), original.signal);
                if (original.signal?.aborted) return Promise.reject(error);
                original.headers.Authorization = `Bearer ${newToken}`;
                return api(original);
            } catch {
                return Promise.reject(error);
            }
        }

        return Promise.reject(error);
    }
);

export default api;

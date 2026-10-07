import axios from "axios";

const baseURL =
    import.meta.env.VITE_API_BASE_URL?.replace(/\/$/, "") ?? "/api";
// - local dev: set VITE_API_BASE_URL=http://localhost:5000/api
// - production: you can use "/api" if you host SPA + API together,
//   or set it to https://your-api.azurewebsites.net/api

const api = axios.create({
    baseURL,
    withCredentials: true,
});

let refreshPromise: Promise<string> | null = null;
let loggingOut = false;

export const setLoggingOut = (value: boolean) => {
    loggingOut = value;
};

// Axios requests and fetch-based streams share the same refresh operation.
export function refreshAccessToken(): Promise<string> {
    if (!refreshPromise) {
        refreshPromise = api.post<{ accessToken: string }>("/auth/refresh")
            .then(({ data }) => {
                localStorage.setItem("token", data.accessToken);
                api.defaults.headers.common.Authorization = `Bearer ${data.accessToken}`;
                return data.accessToken;
            })
            .catch((error: unknown) => {
                localStorage.removeItem("token");
                delete api.defaults.headers.common.Authorization;
                if (!loggingOut) window.location.href = "/login";
                throw error;
            })
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
    }
    return config;
});

api.interceptors.response.use(
    res => res,
    async error => {
        const original = error.config;

        if (loggingOut) return Promise.reject(error);

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
                const newToken = await refreshAccessToken();
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

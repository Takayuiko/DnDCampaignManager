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

let isRefreshing = false;
let refreshQueue: ((token: string) => void)[] = [];
let loggingOut = false;

export const setLoggingOut = (value: boolean) => {
    loggingOut = value;
};

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

        if (error.response?.status === 401 && !original._retry) {
            if (isRefreshing) {
                return new Promise(resolve => {
                    refreshQueue.push(token => {
                        original.headers.Authorization = `Bearer ${token}`;
                        resolve(api(original));
                    });
                });
            }

            original._retry = true;
            isRefreshing = true;

            try {
                const res = await api.post("/auth/refresh");
                const newToken = res.data.accessToken;

                localStorage.setItem("token", newToken);
                api.defaults.headers.common.Authorization = `Bearer ${newToken}`;

                refreshQueue.forEach(cb => cb(newToken));
                refreshQueue = [];

                return api(original);
            } catch {
                localStorage.removeItem("token");
                delete api.defaults.headers.common.Authorization;

                if (!loggingOut) window.location.href = "/login";
                return Promise.reject(error);
            } finally {
                isRefreshing = false;
            }
        }

        return Promise.reject(error);
    }
);

export default api;

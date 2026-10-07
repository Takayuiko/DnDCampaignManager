import api from "./axios";

export const login = (email: string, password: string) =>
    api.post("/auth/login", { email, password });

export const register = (email: string, password: string) =>
    api.post("/auth/register", { email, password });

export const getMe = (signal?: AbortSignal) => api.get("/me", { signal });

import api from "./axios";

export const login = (email: string, password: string) =>
    api.post("/auth/login", { email, password });

export const register = (email: string, password: string) =>
    api.post("/auth/register", { email, password });

export const getDashboard = () =>
    api.get("/dashboard");

export function addPlayerToCampaign(campaignId: number, email: string) {
    return api.post(`/campaigns/${campaignId}/players`, { email });
}

export const getMe = () => api.get("/me");

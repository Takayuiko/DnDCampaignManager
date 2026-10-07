import { createContext, useContext, useEffect, useState } from "react";
import type { ReactNode } from "react";
import api from "../api/axios";
import { getMe } from "../api/authApi";

type User = {
    id: number;
    email: string;
    role: "DM" | "Player";
    isAdmin: boolean;
};

type AuthContextType = {
    user: User | null;
    token: string | null;
    loading: boolean;
    loginWithToken: (token: string) => Promise<void>;
    logout: () => Promise<void>;
    resetAuth: () => void;
};

const AuthContext = createContext<AuthContextType | undefined>(undefined);

export function AuthProvider({ children }: { children: ReactNode }) {
    const [user, setUser] = useState<User | null>(null);
    const [token, setToken] = useState<string | null>(null);
    const [loading, setLoading] = useState(true);

    const resetAuth = () => {
        localStorage.removeItem("token");
        delete api.defaults.headers.common.Authorization;
        setUser(null);
        setToken(null);
    };

    useEffect(() => {
        let generation = 0;
        let active = true;
        let controller: AbortController | null = null;
        const bootstrap = async () => {
            const attempt = ++generation;
            controller?.abort();
            const request = new AbortController();
            controller = request;
            const storedToken = localStorage.getItem("token");

            if (!storedToken) {
                resetAuth();
                setLoading(false);
                return;
            }

            api.defaults.headers.common.Authorization = `Bearer ${storedToken}`;

            try {
                const res = await getMe(request.signal);
                if (!active || attempt !== generation) return;
                setUser(res.data);
                setToken(localStorage.getItem("token"));
            } catch {
                if (active && attempt === generation && localStorage.getItem("token") === storedToken) resetAuth();
            } finally {
                if (active && attempt === generation) setLoading(false);
            }
        };

        bootstrap();
        const synchronize = (event: StorageEvent) => {
            if (event.storageArea === localStorage && (event.key === "token" || event.key === null)) void bootstrap();
        };
        window.addEventListener("storage", synchronize);
        return () => {
            active = false;
            generation++;
            controller?.abort();
            window.removeEventListener("storage", synchronize);
        };
    }, []);

    const loginWithToken = async (newToken: string) => {
        resetAuth();

        localStorage.setItem("token", newToken);
        api.defaults.headers.common.Authorization = `Bearer ${newToken}`;

        try {
            const res = await getMe();
            setUser(res.data);
            setToken(localStorage.getItem("token"));
        } catch {
            resetAuth();
            throw new Error("Invalid token");
        }
    };

    const logout = async () => {
        try {
            await api.post("/auth/logout");
        } catch {
            // ignore
        } finally {
            resetAuth();
        }
    };

    return (
        <AuthContext.Provider
            value={{
                user,
                token,
                loading,
                loginWithToken,
                logout,
                resetAuth
            }}
        >
            {children}
        </AuthContext.Provider>
    );
}

// The provider and its consumer hook intentionally share this module.
// eslint-disable-next-line react-refresh/only-export-components
export function useAuth() {
    const ctx = useContext(AuthContext);
    if (!ctx) {
        throw new Error("useAuth must be used within AuthProvider");
    }
    return ctx;
}

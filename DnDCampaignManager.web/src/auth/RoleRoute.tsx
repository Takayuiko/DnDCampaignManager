import { Navigate } from "react-router-dom";
import { useAuth } from "./AuthContext";
import type { ReactNode } from "react";

type RoleRouteProps = {
    role: "DM" | "Player";
    children: ReactNode;
};

export default function RoleRoute({ role, children }: RoleRouteProps) {
    const { user, loading } = useAuth();

    if (loading) return null;

    if (!user || user.role !== role) {
        return <Navigate to="/dashboard" />;
    }

    return children;
}

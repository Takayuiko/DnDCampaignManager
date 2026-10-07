import { Routes, Route } from "react-router-dom";
import Landing from "./pages/Landing";
import Login from "./pages/Login";
import Register from "./pages/Register";
import Dashboard from "./pages/Dashboard";
import ProtectedRoute from "./auth/ProtectedRoute";
import Navbar from "./components/Navbar";
import { useAuth } from "./auth/AuthContext";
import RoleRoute from "./auth/RoleRoute";
import CreateCampaign from "./pages/CreateCampaign";
import EditCampaign from "./pages/EditCampaign";
import CreateCharacter from "./pages/CreateCharacter";
import EditCharacter from "./pages/EditCharacter";
import AIChat from "./pages/AIChat";
import SessionNotes from "./pages/SessionNotes";
import DungeonMasters from "./pages/DungeonMasters";
import Items from "./pages/Items";
import Maps from "./pages/Maps";

function App() {
    const { loading } = useAuth();

    if (loading) {
        return <div>Loading...</div>;
    }
    return (
        <>
            <Navbar />

            <Routes>
                <Route path="/campaigns/:campaignId/maps" element={<ProtectedRoute><Maps /></ProtectedRoute>} />
                <Route path="/campaigns/:campaignId/items" element={<ProtectedRoute><Items /></ProtectedRoute>} />
                <Route path="/admin/dungeon-masters" element={<RoleRoute role="Admin"><DungeonMasters /></RoleRoute>} />
                <Route path="/" element={<Landing />} />
                <Route path="/login" element={<Login />} />
                <Route path="/register" element={<Register />} />
                <Route
                    path="/dashboard"
                    element={
                        <ProtectedRoute>
                            <Dashboard />
                        </ProtectedRoute>
                    }
                />
                <Route
                    path="/ai"
                    element={
                        <ProtectedRoute>
                            <AIChat />
                        </ProtectedRoute>
                    }
                />
                <Route
                    path="/campaigns/:campaignId/session-notes"
                    element={<ProtectedRoute><SessionNotes /></ProtectedRoute>}
                />
                <Route
                    path="/campaigns/new"
                    element={
                        <RoleRoute role="DM">
                            <CreateCampaign />
                        </RoleRoute>
                    }
                />
                <Route
                    path="/campaigns/:campaignId/edit"
                    element={
                        <RoleRoute role="DM">
                            <EditCampaign />
                        </RoleRoute>
                    }
                />
                <Route
                    path="/campaigns/:campaignId/characters"
                    element={
                        <ProtectedRoute>
                            <CreateCharacter />
                        </ProtectedRoute>
                    }
                />
                <Route
                    path="/campaigns/:campaignId/characters/:characterId/edit"
                    element={
                        <ProtectedRoute>
                            <EditCharacter />
                        </ProtectedRoute>}
                />
            </Routes>
        </>
    );
}

export default App;

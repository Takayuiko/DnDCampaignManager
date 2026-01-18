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

function App() {
    const { loading } = useAuth();

    if (loading) {
        return <div>Loading...</div>;
    }
    return (
        <>
            <Navbar />

            <Routes>
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
                    path="/campaigns/new"
                    element={
                        <RoleRoute role="DM">
                            <CreateCampaign />
                        </RoleRoute>
                    }
                />
                <Route
                    path="/campaigns/:id/edit"
                    element={
                        <RoleRoute role="DM">
                            <EditCampaign />
                        </RoleRoute>
                    }
                />
                <Route
                    path="/campaigns/:id/characters"
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

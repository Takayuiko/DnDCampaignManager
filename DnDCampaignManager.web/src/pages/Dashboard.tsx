import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import { useAuth } from "../auth/AuthContext";
import { getCampaigns, deleteCampaign } from "../api/campaignApi";
import CampaignItem from "../components/CampaignItem";
import Button from "../components/UI/Button";

type Character = {
    id: number;
    name: string;
    userId: number;
};

type Campaign = {
    id: number;
    name: string;
    description: string;
    characters: Character[];
};

export default function Dashboard() {
    const { user, loading } = useAuth();
    const navigate = useNavigate();
    const [campaigns, setCampaigns] = useState<Campaign[]>([]);

    const loadCampaigns = async () => {
        const res = await getCampaigns();
        setCampaigns(res.data);
    };

    useEffect(() => {
        if (loading || !user) return;
            loadCampaigns();
    }, [loading, user]);

    return (
        <div className="min-h-screen bg-gradient-to-b from-stone-100 to-amber-50 px-4 py-8">
            <div className="max-w-6xl mx-auto space-y-8">

                {/* Header */}
                <header className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
                    <div>
                        <h1 className="text-3xl font-bold text-stone-800">
                            📜 Campaign Dashboard
                        </h1>
                        <p className="text-stone-600 text-sm">
                            Welcome, {user?.email}
                        </p>
                    </div>

                    {user?.role === "DM" && (
                        <Button
                            variant="primary"
                            onClick={() => navigate("/campaigns/new")}
                        >
                            + Create Campaign
                        </Button>
                    )}
                </header>

                {/* Campaigns Section */}
                <section>
                    <h2 className="text-xl font-semibold text-stone-700 mb-4">
                        Your Campaigns
                    </h2>

                    {campaigns.length === 0 ? (
                        <div className="text-center text-stone-500 bg-amber-100 border border-amber-200 rounded-lg p-8">
                            No campaigns yet. Start your adventure! 🐉
                        </div>
                    ) : (
                        <ul className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-6">
                            {campaigns.map(campaign => (
                                <CampaignItem
                                    key={campaign.id}
                                    campaign={campaign}
                                    isDM={user?.role === "DM"}
                                    onEdit={id => navigate(`/campaigns/${id}/edit`)}
                                    onDelete={async id => {
                                        await deleteCampaign(id);
                                        loadCampaigns();
                                    }}
                                    onAddCharacter={campaignId =>
                                        navigate(`/campaigns/${campaignId}/characters`)
                                    }
                                />
                            ))}
                        </ul>
                    )}
                </section>
            </div>
        </div>
    );

}

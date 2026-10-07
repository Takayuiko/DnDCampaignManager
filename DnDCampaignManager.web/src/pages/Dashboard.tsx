import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import { useAuth } from "../auth/AuthContext";
import { getCampaigns, deleteCampaign } from "../api/campaignApi";
import CampaignItem from "../components/CampaignItem";
import Button from "../components/UI/Button";
import ErrorPanel from "../components/UI/ErrorPanel";
import { extractApiError } from "../Utils/apiError";

type Character = {
    id: number;
    name: string;
    userId: number;
};

type Campaign = {
    id: number;
    name: string;
    description: string;
    ownerId: number;
    characters: Character[];
};

export default function Dashboard() {
    const { user, loading } = useAuth();
    const navigate = useNavigate();
    const [campaigns, setCampaigns] = useState<Campaign[]>([]);
    const [campaignsLoading, setCampaignsLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);
    const isDM = user?.role === "DM";

    const loadCampaigns = async () => {
        setCampaignsLoading(true);
        setError(null);
        try {
            const res = await getCampaigns();
            setCampaigns(res.data);
        } catch (err: unknown) {
            setError(extractApiError(err, "Unable to load campaigns."));
        } finally { setCampaignsLoading(false); }
    };

    useEffect(() => {
        if (loading || !user) return;
            loadCampaigns();
    }, [loading, user]);

    return (
        <div className="min-h-screen bg-gradient-to-b from-stone-100 to-amber-50 px-4 py-8">
            <div className="max-w-7xl mx-auto space-y-8">

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

                    {isDM && (
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

                    {error && <ErrorPanel message={error} />}
                    {campaignsLoading ? <p className="p-6 text-stone-600">Loading campaigns...</p> : campaigns.length === 0 ? (
                        <div className="text-center text-stone-500 bg-amber-100 border border-amber-200 rounded-lg p-8">
                            No campaigns yet. Start your adventure! 🐉
                        </div>
                    ) : (
                        <ul className="flex flex-col gap-8">
                            {campaigns.map(campaign => (
                                <CampaignItem
                                    key={campaign.id}
                                    campaign={campaign}
                                    isDM={isDM && campaign.ownerId === user?.id}
                                    onEdit={id => navigate(`/campaigns/${id}/edit`)}
                                    onDelete={async id => {
                                        try {
                                            await deleteCampaign(id);
                                            await loadCampaigns();
                                        } catch (err: unknown) {
                                            setError(extractApiError(err, "Unable to delete campaign."));
                                        }
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

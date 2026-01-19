import { useEffect, useMemo, useState } from "react";
import { useNavigate, useParams } from "react-router-dom";
import {
    getCampaign,
    updateCampaign,
    addPlayerToCampaign,
    removePlayerFromCampaign,
} from "../api/campaignApi";
import { useAuth } from "../auth/AuthContext";

type Player = { id: number; email: string };

type CampaignResponse = {
    id: number;
    name: string;
    description: string;
    ownerId: number;
    players: Player[];
};

type TabKey = "campaign" | "players";

export default function EditCampaign() {
    const { id } = useParams();
    const campaignId = Number(id);
    const navigate = useNavigate();
    const { user } = useAuth();

    const isDM = user?.role === "DM";

    const [tab, setTab] = useState<TabKey>("campaign");

    // campaign fields
    const [name, setName] = useState("");
    const [description, setDescription] = useState("");

    // players
    const [players, setPlayers] = useState<Player[]>([]);
    const [playerEmail, setPlayerEmail] = useState("");

    // UI state
    const [loading, setLoading] = useState(true);
    const [savingCampaign, setSavingCampaign] = useState(false);
    const [inviting, setInviting] = useState(false);
    const [removingId, setRemovingId] = useState<number | null>(null);

    const [error, setError] = useState<string | null>(null);
    const [playersError, setPlayersError] = useState<string | null>(null);
    const [saveSuccess, setSaveSuccess] = useState<string | null>(null);

    const loadCampaign = async () => {
        if (!campaignId) return;

        setError(null);
        setPlayersError(null);

        const res = await getCampaign(campaignId);
        const c: CampaignResponse = res.data;

        setName(c.name ?? "");
        setDescription(c.description ?? "");
        setPlayers(Array.isArray(c.players) ? c.players : []);
    };

    useEffect(() => {
        const run = async () => {
            try {
                setLoading(true);
                await loadCampaign();
            } catch (err: any) {
                alert(err?.response?.data ?? "Campaign not found");
                navigate("/dashboard");
            } finally {
                setLoading(false);
            }
        };

        run();
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [campaignId]);

    const canInviteThisEmail = useMemo(() => {
        const e = playerEmail.trim().toLowerCase();
        if (!e) return false;
        if (players.some(p => p.email.toLowerCase() === e)) return false;
        return true;
    }, [playerEmail, players]);

    const submitCampaign = async (e: React.FormEvent) => {
        e.preventDefault();
        if (!campaignId) return;

        setError(null);
        setSavingCampaign(true);
        try {
            await updateCampaign(campaignId, { name, description });
            setSaveSuccess("Campaign saved.");
            await loadCampaign();
        } catch (err: any) {
            setError(err?.response?.data ?? "Failed to update campaign");
        } finally {
            setSavingCampaign(false);
        }
    };

    const invitePlayer = async () => {
        if (!campaignId) return;

        setPlayersError(null);

        const email = playerEmail.trim().toLowerCase();
        if (!email) {
            setPlayersError("Please enter an email.");
            return;
        }

        setInviting(true);
        try {
            await addPlayerToCampaign(campaignId, email);
            setPlayerEmail("");
            await loadCampaign(); 
        } catch (err: any) {
            setPlayersError(err?.response?.data ?? "Failed to invite player");
        } finally {
            setInviting(false);
        }
    };

    const removePlayer = async (playerId: number) => {
        if (!campaignId) return;

        setPlayersError(null);
        setRemovingId(playerId);

        try {
            await removePlayerFromCampaign(campaignId, playerId);
            await loadCampaign();
        } catch (err: any) {
            setPlayersError(err?.response?.data ?? "Failed to remove player");
        } finally {
            setRemovingId(null);
        }
    };

    if (loading) return <p className="p-6 text-stone-600">Loading...</p>;

    return (
        <div className="min-h-screen bg-stone-100 px-4 py-8">
            <div className="max-w-5xl mx-auto bg-amber-50 border border-stone-300 rounded-xl shadow-lg p-6 space-y-6">
                <div className="flex items-start justify-between gap-4">
                    <div>
                        <h1 className="text-3xl font-bold text-stone-800">🛡️ Manage Campaign</h1>
                        <p className="text-stone-600 text-sm">
                            Update your campaign details and manage players.
                        </p>
                    </div>

                    <button
                        type="button"
                        onClick={() => navigate("/dashboard")}
                        className="px-3 py-2 rounded-md border border-stone-300 bg-stone-100 hover:bg-stone-200 text-stone-700"
                    >
                        Back
                    </button>
                </div>

                {/* Tabs */}
                <div className="flex gap-2 border-b border-stone-300 pb-2">
                    <button
                        type="button"
                        onClick={() => setTab("campaign")}
                        className={`px-3 py-2 rounded-md text-sm font-semibold ${tab === "campaign"
                                ? "bg-stone-900 text-amber-50"
                                : "bg-stone-100 hover:bg-stone-200 text-stone-700 border border-stone-300"
                            }`}
                    >
                        Campaign
                    </button>

                    <button
                        type="button"
                        onClick={() => setTab("players")}
                        className={`px-3 py-2 rounded-md text-sm font-semibold ${tab === "players"
                                ? "bg-stone-900 text-amber-50"
                                : "bg-stone-100 hover:bg-stone-200 text-stone-700 border border-stone-300"
                            }`}
                        disabled={!isDM}
                        title={!isDM ? "Only DMs can manage players" : undefined}
                    >
                        Players
                    </button>
                </div>

                {/* Campaign tab */}
                {tab === "campaign" && (
                    <section className="bg-stone-50 border border-stone-300 rounded-lg p-4">
                        <h2 className="text-lg font-semibold text-stone-700 mb-4">Campaign Details</h2>

                        {error && (
                            <div className="mb-4 rounded-lg border border-red-300 bg-red-50 px-4 py-3 text-sm text-red-800">
                                {error}
                            </div>
                        )}

                        {saveSuccess && (
                            <div className="mb-4 rounded-lg border border-emerald-300 bg-emerald-50 px-4 py-3 text-sm text-emerald-800">
                                {saveSuccess}
                            </div>
                        )}

                        <form onSubmit={submitCampaign} className="space-y-4">
                            <div>
                                <label className="block text-sm font-semibold text-stone-700">Name</label>
                                <input
                                    value={name}
                                    onChange={e => setName(e.target.value)}
                                    className="w-full border border-stone-400 rounded-md p-2 bg-white"
                                    required
                                />
                            </div>

                            <div>
                                <label className="block text-sm font-semibold text-stone-700">Description</label>
                                <textarea
                                    value={description}
                                    onChange={e => setDescription(e.target.value)}
                                    className="w-full border border-stone-400 rounded-md p-2 bg-white min-h-[120px]"
                                />
                            </div>

                            <div className="flex justify-end gap-3">
                                <button
                                    type="button"
                                    onClick={() => navigate("/dashboard")}
                                    className="px-4 py-2 rounded-md border border-stone-400 bg-stone-200 hover:bg-stone-300"
                                    disabled={savingCampaign}
                                >
                                    Cancel
                                </button>
                                { isDM && (
                                    <button
                                        type="submit"
                                        className="px-5 py-2 rounded-md bg-emerald-700 text-white font-semibold hover:bg-emerald-600 disabled:opacity-60">
                                        {savingCampaign ? "Saving..." : "Save"}
                                </button>
                                )}
                            </div>
                        </form>
                    </section>
                )}

                {/* Players tab (DM only) */}
                {tab === "players" && (
                    <section className="bg-stone-50 border border-stone-300 rounded-lg p-4">
                        <div className="flex items-center justify-between mb-4">
                            <h2 className="text-lg font-semibold text-stone-700">Players</h2>
                            <span className="text-xs text-stone-600">
                                {players.length} player{players.length === 1 ? "" : "s"}
                            </span>
                        </div>

                        {!isDM ? (
                            <p className="text-stone-600 text-sm">
                                Only the DM can invite or remove players.
                            </p>
                        ) : (
                            <>
                                {playersError && (
                                    <div className="mb-4 rounded-lg border border-red-300 bg-red-50 px-4 py-3 text-sm text-red-800">
                                        {playersError}
                                    </div>
                                )}

                                {/* Invite by email */}
                                <div className="flex flex-col sm:flex-row gap-3">
                                    <input
                                        value={playerEmail}
                                        onChange={e => setPlayerEmail(e.target.value)}
                                        placeholder="player@email.com"
                                        className="flex-1 border border-stone-400 rounded-md p-2 bg-white"
                                    />

                                    <button
                                        type="button"
                                        onClick={invitePlayer}
                                        disabled={!canInviteThisEmail || inviting}
                                        className="px-4 py-2 rounded-md bg-red-800 text-white font-semibold hover:bg-red-700 disabled:opacity-60">
                                        {inviting ? "Inviting..." : "Invite"}
                                    </button>
                                </div>

                                <p className="mt-2 text-xs text-stone-600">
                                    Tip: the invited email must already be registered in your app.
                                </p>

                                {/* Player list */}
                                <div className="mt-5">
                                    {players.length === 0 ? (
                                        <p className="text-stone-600 text-sm">No players yet.</p>
                                    ) : (
                                        <ul className="divide-y divide-stone-200 border border-stone-200 rounded-md bg-white">
                                            {players.map(p => (
                                                <li key={p.id} className="flex items-center justify-between px-3 py-2">
                                                    <div className="min-w-0">
                                                        <div className="font-semibold text-stone-800 truncate">{p.email}</div>
                                                        <div className="text-xs text-stone-500">User ID: {p.id}</div>
                                                    </div>

                                                    <button
                                                        type="button"
                                                        onClick={() => {
                                                            if (confirm(`Remove ${p.email} from this campaign?`)) {
                                                                removePlayer(p.id);
                                                            }
                                                        }}
                                                        disabled={removingId === p.id}
                                                        className="px-3 py-1.5 rounded-md border border-stone-300 bg-stone-100 hover:bg-stone-200 text-stone-700 disabled:opacity-60"
                                                    >
                                                        {removingId === p.id ? "Removing..." : "Remove"}
                                                    </button>
                                                </li>
                                            ))}
                                        </ul>
                                    )}
                                </div>
                            </>
                        )}
                    </section>
                )}
            </div>
        </div>
    );
}

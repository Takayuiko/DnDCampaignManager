import { useEffect, useState } from "react";
import { useNavigate, useParams } from "react-router-dom";
import {
    getCampaign,
    updateCampaign,
    addPlayerToCampaign
} from "../api/campaignApi";

export default function EditCampaign() {
    const { id } = useParams();
    const navigate = useNavigate();

    const [name, setName] = useState("");
    const [description, setDescription] = useState("");
    const [loading, setLoading] = useState(true);

    const [playerEmail, setPlayerEmail] = useState("");
    const [addingPlayer, setAddingPlayer] = useState(false);

    useEffect(() => {
        if (!id) return;

        getCampaign(Number(id))
            .then(res => {
                setName(res.data.name);
                setDescription(res.data.description);
            })
            .catch(() => {
                alert("Campaign not found");
                navigate("/dashboard");
            })
            .finally(() => setLoading(false));
    }, [id, navigate]);

    const handleSubmit = async (e: React.FormEvent) => {
        e.preventDefault();

        try {
            await updateCampaign(Number(id), {
                name,
                description
            });
            navigate("/dashboard");
        } catch {
            alert("Failed to update campaign");
        }
    };

    const handleAddPlayer = async () => {
        if (!playerEmail.trim()) return;

        try {
            setAddingPlayer(true);
            await addPlayerToCampaign(Number(id), playerEmail);
            setPlayerEmail("");
            alert("Player added");
        } catch (err: any) {
            alert(err?.response?.data ?? "Failed to add player");
        } finally {
            setAddingPlayer(false);
        }
    };

    if (loading) return <p>Loading...</p>;

    return (
        <div>
            <h2>Edit Campaign</h2>

            {/* 🎯 Campaign Details */}
            <form onSubmit={handleSubmit}>
                <input
                    value={name}
                    onChange={e => setName(e.target.value)}
                    placeholder="Name"
                />

                <textarea
                    value={description}
                    onChange={e => setDescription(e.target.value)}
                    placeholder="Description"
                />

                <button type="submit">Save</button>
                <button type="button" onClick={() => navigate("/dashboard")}>
                    Cancel
                </button>
            </form>

            <hr />

            <section>
                <h3>Add Player</h3>

                <input
                    value={playerEmail}
                    onChange={e => setPlayerEmail(e.target.value)}
                    placeholder="Player email"
                />

                <button
                    type="button"
                    onClick={handleAddPlayer}
                    disabled={addingPlayer}
                >
                    {addingPlayer ? "Adding..." : "Add Player"}
                </button>
            </section>
        </div>
    );
}

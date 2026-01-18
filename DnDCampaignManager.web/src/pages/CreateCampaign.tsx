import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { createCampaign } from "../api/campaignApi";

export default function CreateCampaign() {
    const navigate = useNavigate();
    const [name, setName] = useState("");
    const [description, setDescription] = useState("");

    const submit = async (e: React.FormEvent) => {
        e.preventDefault();
        await createCampaign({ name, description });
        navigate("/dashboard");
    };

    return (
        <form onSubmit={submit}>
            <h2>Create Campaign</h2>

            <input
                value={name}
                onChange={e => setName(e.target.value)}
                placeholder="Campaign name"
                required
            />

            <textarea
                value={description}
                onChange={e => setDescription(e.target.value)}
                placeholder="Description"
            />

            <button type="submit">Create</button>
        </form>
    );
}

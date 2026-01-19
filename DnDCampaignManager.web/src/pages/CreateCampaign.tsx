import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { createCampaign } from "../api/campaignApi";
import Button from "../components/UI/Button";

function extractApiError(err: any, fallback: string) {
    const data = err?.response?.data;
    if (typeof data === "string") return data;

    if (data?.errors && typeof data.errors === "object") {
        const parts: string[] = [];
        for (const key of Object.keys(data.errors)) {
            const msgs = data.errors[key];
            if (Array.isArray(msgs)) parts.push(...msgs);
        }
        if (parts.length) return parts.join(" ");
    }

    return fallback;
}

export default function CreateCampaign() {
    const navigate = useNavigate();

    const [name, setName] = useState("");
    const [description, setDescription] = useState("");

    const [submitting, setSubmitting] = useState(false);
    const [error, setError] = useState<string | null>(null);

    const submit = async (e: React.FormEvent) => {
        e.preventDefault();
        setError(null);
        setSubmitting(true);

        try {
            await createCampaign({ name, description });
            navigate("/dashboard");
        } catch (err: any) {
            setError(extractApiError(err, "Failed to create campaign."));
        } finally {
            setSubmitting(false);
        }
    };

    return (
        <div className="min-h-screen bg-stone-100 p-4 md:p-8">
            <div className="max-w-3xl mx-auto bg-amber-50 border border-stone-300 rounded-xl shadow-lg p-6">
                <h1 className="text-3xl font-bold text-stone-800 mb-2">🗺️ New Campaign</h1>
                <p className="text-stone-600 mb-6">Set the stage for your next adventure.</p>

                {error && (
                    <div className="mb-4 rounded-lg border border-red-300 bg-red-50 px-4 py-3 text-sm text-red-800">
                        {error}
                    </div>
                )}

                <form onSubmit={submit} className="space-y-4">
                    <div>
                        <label className="block text-sm font-semibold text-stone-700">
                            Campaign Name
                        </label>
                        <input
                            value={name}
                            onChange={e => { setName(e.target.value); setError(null); }}
                            placeholder="Curse of Strahd..."
                            className="mt-1 w-full rounded-lg border border-stone-300 bg-white px-3 py-2 text-stone-900 shadow-sm focus:border-emerald-600 focus:outline-none focus:ring-2 focus:ring-emerald-200"
                            required
                        />
                    </div>

                    <div>
                        <label className="block text-sm font-semibold text-stone-700">
                            Description
                        </label>
                        <textarea
                            value={description}
                            onChange={e => { setDescription(e.target.value); setError(null); }}
                            placeholder="What’s the hook? Who’s the villain?"
                            className="mt-1 w-full min-h-[120px] rounded-lg border border-stone-300 bg-white px-3 py-2 text-stone-900 shadow-sm focus:border-emerald-600 focus:outline-none focus:ring-2 focus:ring-emerald-200"
                        />
                    </div>

                    <div className="flex justify-end gap-3 pt-2">
                        <Button type="button" variant="ghost" onClick={() => navigate("/dashboard")}>
                            Cancel
                        </Button>
                        <Button type="submit" variant="primary" disabled={submitting}>
                            {submitting ? "Creating..." : "Create Campaign"}
                        </Button>
                    </div>
                </form>
            </div>
        </div>
    );
}

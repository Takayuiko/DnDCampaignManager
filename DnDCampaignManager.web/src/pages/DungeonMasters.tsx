import { type FormEvent, useEffect, useState } from "react";
import { getDungeonMasters, promoteDungeonMaster, previewDmRemoval, removeDungeonMaster, type DungeonMaster, type DmRemovalPreview } from "../api/dungeonMastersApi";
import { extractApiError } from "../Utils/apiError";

export default function DungeonMasters() {
    const [dms, setDms] = useState<DungeonMaster[]>([]);
    const [email, setEmail] = useState("");
    const [loading, setLoading] = useState(true);
    const [busy, setBusy] = useState(false);
    const [error, setError] = useState("");
    const [notice, setNotice] = useState("");
    const [preview, setPreview] = useState<DmRemovalPreview | null>(null);

    useEffect(() => {
        let active = true;
        const controller = new AbortController();
        void getDungeonMasters(controller.signal).then(response => { if (active) setDms(response.data); })
            .catch(() => { if (active) setError("Unable to load Dungeon Masters. Reload to retry."); })
            .finally(() => { if (active) setLoading(false); });
        return () => { active = false; controller.abort(); };
    }, []);

    async function promote(event: FormEvent) {
        event.preventDefault();
        if (busy || !email.trim()) return;
        setBusy(true); setError(""); setNotice(""); setPreview(null);
        try {
            const response = await promoteDungeonMaster(email.trim());
            setDms(current => [...current.filter(dm => dm.id !== response.data.id), response.data].sort((a, b) => a.email.localeCompare(b.email)));
            setEmail(""); setNotice("DM rights granted. The user must sign in again to use them.");
        } catch (requestError) { setError(extractApiError(requestError, "Unable to grant DM rights.")); }
        finally { setBusy(false); }
    }

    async function review(id: number) {
        setBusy(true); setError(""); setNotice(""); setPreview(null);
        try { setPreview((await previewDmRemoval(id)).data); }
        catch (requestError) { setError(extractApiError(requestError, "Unable to review DM removal.")); }
        finally { setBusy(false); }
    }

    async function remove() {
        if (!preview || busy) return;
        setBusy(true); setError(""); setNotice("");
        try {
            await removeDungeonMaster(preview.userId, preview.campaignCount);
            setDms(current => current.filter(dm => dm.id !== preview.userId));
            setPreview(null); setNotice("DM rights removed and owned campaign data deleted. Their player characters in other campaigns are preserved.");
        } catch (requestError) {
            setError(extractApiError(requestError, "Unable to remove DM rights."));
            setPreview(null);
        } finally { setBusy(false); }
    }

    return <main className="min-h-screen bg-amber-50 px-4 py-6">
        <div className="mx-auto max-w-3xl space-y-4">
            <h1 className="text-3xl font-bold text-stone-800">Manage Dungeon Masters</h1>
            <p className="text-sm text-stone-600">Grant DM rights to an existing registered user. Admin permission is assigned only through development provisioning.</p>
            {error && <p role="alert" className="rounded border border-red-200 bg-red-50 p-3 text-red-800">{error}</p>}
            {notice && <p role="status" className="rounded border border-green-200 bg-green-50 p-3">{notice}</p>}
            <form onSubmit={promote} className="flex flex-wrap items-end gap-3 rounded-xl border bg-white p-4">
                <label className="flex-1 text-sm">Registered user email
                    <input type="email" required value={email} disabled={busy || loading} onChange={e => setEmail(e.target.value)} className="mt-1 block w-full rounded border p-2" />
                </label>
                <button disabled={busy || loading || !email.trim()} className="rounded bg-stone-800 px-4 py-2 text-white disabled:opacity-50">Grant DM rights</button>
            </form>
            {loading ? <p>Loading Dungeon Masters…</p> : <ul className="space-y-3">
                {dms.map(dm => <li key={dm.id} className="flex flex-wrap items-center justify-between gap-3 rounded-xl border bg-white p-4">
                    <div><p className="font-semibold">{dm.email}{dm.isAdmin && " · Administrator"}</p><p className="text-xs text-stone-600">{dm.campaignCount} owned campaigns</p></div>
                    {!dm.isAdmin && <button disabled={busy} onClick={() => void review(dm.id)} className="rounded border border-red-300 px-3 py-2 text-sm text-red-800 disabled:opacity-50">Review removal</button>}
                </li>)}
            </ul>}
            {preview && <section className="space-y-3 rounded-xl border border-red-300 bg-red-50 p-5">
                <h2 className="font-bold">Remove DM rights from {preview.email}?</h2>
                <p className="text-sm">This permanently deletes {preview.campaignCount} owned campaigns, {preview.characterCount} characters in those campaigns,
                    {" "}{preview.sessionNoteCount} session notes and their search indexes, {preview.campaignConversationCount} campaign conversations,
                    and {preview.generalConversationCount} general conversations. Campaign memberships are also removed.</p>
                <p className="text-sm">The account becomes a player. Their {preview.preservedCharacterCount} characters in other campaigns and their player memberships remain. Existing login sessions are invalidated.</p>
                <div className="flex gap-3">
                    <button disabled={busy} onClick={() => void remove()} className="rounded bg-red-800 px-4 py-2 text-white disabled:opacity-50">Confirm removal and delete owned campaign data</button>
                    <button disabled={busy} onClick={() => setPreview(null)} className="rounded border bg-white px-4 py-2">Cancel</button>
                </div>
            </section>}
        </div>
    </main>;
}

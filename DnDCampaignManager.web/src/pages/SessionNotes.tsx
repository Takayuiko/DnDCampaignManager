import { type FormEvent, useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { useAuth } from "../auth/AuthContext";
import { getCampaigns, type CampaignSummary } from "../api/campaignApi";
import { createSessionNote, deleteSessionNote, getSessionNotes, indexSessionNote, transcribeSessionAudio, type SessionNote } from "../api/sessionNotesApi";
import { extractApiError } from "../Utils/apiError";

export default function SessionNotes() {
    const { campaignId: routeId } = useParams();
    const campaignId = Number(routeId);
    const { user } = useAuth();
    const [campaign, setCampaign] = useState<CampaignSummary | null>(null);
    const [notes, setNotes] = useState<SessionNote[]>([]);
    const [loading, setLoading] = useState(true);
    const [busy, setBusy] = useState(false);
    const [error, setError] = useState("");
    const [notice, setNotice] = useState("");
    const [sessionNumber, setSessionNumber] = useState(1);
    const [title, setTitle] = useState("");
    const [content, setContent] = useState("");
    const [playedOn, setPlayedOn] = useState(() => {
        const now = new Date();
        return `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, "0")}-${String(now.getDate()).padStart(2, "0")}`;
    });
    const canManage = user?.role === "DM" && user.id === campaign?.ownerId;
    const [audio, setAudio] = useState<File | null>(null);
    const [transcribing, setTranscribing] = useState(false);

    async function transcribe() {
        if (!audio || busy || !canManage) return;
        if (audio.size === 0 || audio.size > 25000000) {
            setError("Choose a nonempty audio file up to 25 MB. Compress larger recordings or split them into parts.");
            return;
        }
        if (content.length > 0 && !confirm("Replace the current draft notes with the transcript?")) return;
        setBusy(true); setTranscribing(true); setError(""); setNotice("");
        try {
            const response = await transcribeSessionAudio(campaignId, audio);
            setContent(response.data.text);
            setNotice("Transcript ready. Review names and remove out-of-game discussion, then Save session to add it to campaign knowledge.");
        } catch (requestError) {
            setError(extractApiError(requestError, "Unable to transcribe the recording. Your existing draft is unchanged. Please try again."));
        } finally { setBusy(false); setTranscribing(false); }
    }

    useEffect(() => {
        let active = true;
        void Promise.all([getCampaigns(), getSessionNotes(campaignId)]).then(([list, sessions]) => {
            if (!active) return;
            setCampaign(list.data.find(c => c.id === campaignId) ?? null);
            setNotes(sessions.data);
            setSessionNumber(Math.max(0, ...sessions.data.map(n => n.sessionNumber)) + 1);
        }).catch(() => { if (active) setError("Unable to load session notes. Check your campaign access and reload."); })
            .finally(() => { if (active) setLoading(false); });
        return () => { active = false; };
    }, [campaignId]);

    async function save(event: FormEvent) {
        event.preventDefault();
        if (busy || !canManage) return;
        setBusy(true); setError(""); setNotice("");
        try {
            const response = await createSessionNote(campaignId, { sessionNumber, title: title.trim(), content: content.trim(), playedOn });
            setNotes(current => [response.data, ...current].sort((a, b) => b.sessionNumber - a.sessionNumber || b.id - a.id));
            setTitle(""); setContent(""); setSessionNumber(current => current + 1);
            setNotice(response.data.indexStatus === "ready" ? "Session saved and ready for campaign chat."
                : "Session saved. Search indexing did not finish; use Retry indexing below.");
        } catch (requestError) { setError(extractApiError(requestError, "Unable to save session notes. Please try again.")); }
        finally { setBusy(false); }
    }

    async function retry(noteId: number) {
        setBusy(true); setError(""); setNotice("");
        try {
            const response = await indexSessionNote(campaignId, noteId);
            setNotes(current => current.map(note => note.id === noteId ? response.data : note));
            setNotice(response.data.indexStatus === "ready" ? "Session is ready for campaign chat."
                : "Notes are saved, but indexing failed. Please try again later.");
        } catch { setError("Unable to index these notes. Please try again."); }
        finally { setBusy(false); }
    }

    async function remove(noteId: number) {
        if (!confirm("Delete these session notes and their search index?")) return;
        setBusy(true); setError(""); setNotice("");
        try {
            await deleteSessionNote(campaignId, noteId);
            setNotes(current => current.filter(note => note.id !== noteId));
            setNotice("Session notes deleted. Earlier chat replies retain their retrieved references.");
        } catch { setError("Unable to delete these notes."); }
        finally { setBusy(false); }
    }

    return <main className="min-h-screen bg-amber-50 px-4 py-6">
        <div className="mx-auto max-w-4xl space-y-4">
            <Link className="text-sm underline" to="/dashboard">Back to campaigns</Link>
            <h1 className="text-3xl font-bold text-stone-800">{campaign?.name ?? "Campaign"} · Session notes</h1>
            <p className="text-sm text-stone-600">Record what happened during play. Saved notes are shared with campaign members and can be used in campaign AI conversations.</p>
            <Link className="inline-block rounded bg-stone-800 px-4 py-2 text-sm text-white" to="/ai">Open AI chat</Link>
            {error && <div role="alert" className="rounded border border-red-200 bg-red-50 p-3 text-red-800">{error}</div>}
            {notice && <div role="status" className="rounded border border-amber-300 bg-white p-3">{notice}</div>}
            {loading && <p>Loading session notes…</p>}
            {!loading && canManage && <form onSubmit={save} className="space-y-3 rounded-xl border border-stone-300 bg-white p-5">
                <h2 className="text-lg font-semibold">Add a session</h2>
                <div className="space-y-2 rounded border border-stone-200 bg-stone-50 p-3">
                    <label className="block text-sm">Start from an audio recording
                        <input type="file" accept=".mp3,.m4a,.wav,.webm,.mpeg,.mpga" disabled={busy}
                            onChange={event => setAudio(event.target.files?.[0] ?? null)} className="mt-2 block w-full text-sm" />
                    </label>
                    <p className="text-xs text-stone-600">Up to 25 MB. Audio is sent to OpenAI for transcription. Review the text before saving; the recording itself is not retained.</p>
                    <button type="button" onClick={() => void transcribe()} disabled={busy || !audio}
                        className="rounded bg-stone-700 px-3 py-2 text-sm text-white disabled:opacity-50">
                        {transcribing ? "Transcribing… this may take several minutes" : "Transcribe audio"}
                    </button>
                </div>
                <div className="flex flex-wrap gap-4">
                    <label className="text-sm">Session number
                        <input required type="number" min={1} max={100000} value={sessionNumber} disabled={busy}
                            onChange={event => setSessionNumber(Number(event.target.value))} className="mt-1 block rounded border p-2" />
                    </label>
                    <label className="text-sm">Date played
                        <input required type="date" value={playedOn} disabled={busy} onChange={event => setPlayedOn(event.target.value)} className="mt-1 block rounded border p-2" />
                    </label>
                </div>
                <label className="block text-sm">Title
                    <input required maxLength={160} value={title} disabled={busy} onChange={event => setTitle(event.target.value)} className="mt-1 w-full rounded border p-2" />
                </label>
                <label className="block text-sm">What happened?
                    <textarea required maxLength={240000} rows={8} value={content} disabled={busy} onChange={event => setContent(event.target.value)}
                        placeholder="Events, discoveries, NPCs encountered, decisions and unresolved threads…" className="mt-1 w-full rounded border p-2" />
                </label>
                <p className="text-xs text-stone-500">{content.length.toLocaleString()} / 240,000 characters. These notes are visible to players; omit DM secrets.</p>
                <button disabled={busy || !title.trim() || !content.trim()} className="rounded bg-stone-800 px-4 py-2 text-white disabled:opacity-50">
                    {busy ? "Working…" : "Save session"}
                </button>
            </form>}
            {!loading && notes.length === 0 && <p className="rounded bg-white p-4">No session notes yet. The campaign DM can add the first session.</p>}
            {notes.map(note => <article key={note.id} className="space-y-2 rounded-xl border border-stone-300 bg-white p-5">
                <h2 className="text-lg font-semibold">Session {note.sessionNumber}: {note.title}</h2>
                <p className="text-xs text-stone-500">{note.playedOn} · {note.indexStatus === "ready" ? "Searchable" : "Not searchable yet"}
                    {note.indexStatus === "ready" && ` · ${note.chunkCount} passages · ${note.embeddingInputTokens.toLocaleString()} embedding input tokens`}</p>
                <p className="whitespace-pre-wrap text-sm text-stone-700">{note.content}</p>
                {canManage && <div className="flex gap-4 text-xs">
                    <button disabled={busy} onClick={() => void retry(note.id)} className="underline disabled:opacity-50">{note.indexStatus === "ready" ? "Reindex" : "Retry indexing"}</button>
                    <button disabled={busy} onClick={() => void remove(note.id)} className="text-red-700 underline disabled:opacity-50">Delete</button>
                </div>}
            </article>)}
        </div>
    </main>;
}

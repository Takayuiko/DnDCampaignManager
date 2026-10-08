import { useEffect, useRef, useState, type FormEvent } from "react";
import { Link, useParams } from "react-router-dom";
import { getMaps, type CampaignMap } from "../api/mapsApi";
import { deleteNpc, getNpcImage, getNpcThumbnail, getNpcs, saveNpc, type CampaignNpc, type NpcCatalog, type NpcDraft } from "../api/npcsApi";
import Button from "../components/UI/Button";
import { extractApiError } from "../Utils/apiError";

const empty = (): NpcDraft => ({ name: "", description: "", isPartyMember: false, mapId: null, locationName: "" });
const field = "w-full rounded border border-stone-300 bg-white p-2";

function Portrait({ npc }: { npc: CampaignNpc }) {
    const [url, setUrl] = useState("");
    const [error, setError] = useState(false);
    const [expanded, setExpanded] = useState(false);
    useEffect(() => {
        const controller = new AbortController();
        let objectUrl = "";
        void Promise.resolve().then(async () => {
            if (controller.signal.aborted) return;
            setUrl(""); setError(false);
            if (!npc.hasImage) return;
            const response = await getNpcThumbnail(npc.campaignId, npc.id, controller.signal);
            if (controller.signal.aborted) return;
            objectUrl = URL.createObjectURL(response.data); setUrl(objectUrl);
        }).catch(() => { if (!controller.signal.aborted) setError(true); });
        return () => { controller.abort(); if (objectUrl) URL.revokeObjectURL(objectUrl); };
    }, [npc.campaignId, npc.id, npc.hasImage, npc.version]);
    return !npc.hasImage ? null : error ? <p className="text-sm" role="alert">Portrait unavailable.</p> :
        url ? <>
            <button type="button" className="block w-full rounded-xl bg-stone-100 p-3 focus-visible:outline-2 focus-visible:outline-red-800" aria-label={`Open original portrait of ${npc.name}`} onClick={() => setExpanded(true)}>
                <img src={url} alt={npc.name} className="h-64 w-full rounded-lg object-contain" onError={() => setError(true)} />
                <span className="mt-2 block text-sm text-stone-600">Click to view original portrait</span>
            </button>
            {expanded && <OriginalPortrait npc={npc} onClose={() => setExpanded(false)} />}
        </> : <p>Loading portrait…</p>;
}

function OriginalPortrait({ npc, onClose }: { npc: CampaignNpc; onClose: () => void }) {
    const dialog = useRef<HTMLDialogElement>(null);
    const [url, setUrl] = useState("");
    const [error, setError] = useState(false);
    useEffect(() => {
        dialog.current?.showModal();
        const controller = new AbortController();
        let objectUrl = "";
        void getNpcImage(npc.campaignId, npc.id, controller.signal).then(response => {
            if (controller.signal.aborted) return;
            objectUrl = URL.createObjectURL(response.data); setUrl(objectUrl);
        }).catch(() => { if (!controller.signal.aborted) setError(true); });
        return () => { controller.abort(); if (objectUrl) URL.revokeObjectURL(objectUrl); };
    }, [npc.campaignId, npc.id]);
    return <dialog ref={dialog} onCancel={onClose} onClose={onClose} aria-label={`${npc.name} original portrait`}
        className="m-auto max-h-[95vh] w-[min(95vw,1200px)] rounded-xl bg-white p-5 backdrop:bg-black/70">
        <div className="mb-4 flex items-center justify-between gap-4"><h2 className="text-xl font-semibold">{npc.name}</h2>
            <Button type="button" variant="secondary" onClick={onClose}>Close</Button></div>
        {error ? <p role="alert">Unable to load the original portrait. Close and try again.</p> : url ?
            <img src={url} alt={npc.name} className="mx-auto max-h-[80vh] max-w-full object-contain" onError={() => setError(true)} /> : <p role="status">Loading original portrait…</p>}
    </dialog>;
}

export default function Npcs() {
    const campaignId = Number(useParams().campaignId);
    const [catalog, setCatalog] = useState<NpcCatalog | null>(null);
    const [maps, setMaps] = useState<CampaignMap[]>([]);
    const [loading, setLoading] = useState(true);
    const [busy, setBusy] = useState(false);
    const [error, setError] = useState("");
    const [notice, setNotice] = useState("");
    const [editing, setEditing] = useState<CampaignNpc | null>(null);
    const [open, setOpen] = useState(false);
    const [draft, setDraft] = useState<NpcDraft>(empty);
    const [image, setImage] = useState<File | null>(null);
    const [removeImage, setRemoveImage] = useState(false);
    const [formKey, setFormKey] = useState(0);
    const [search, setSearch] = useState("");
    const [partyOnly, setPartyOnly] = useState(false);
    const scope = useRef<AbortController | null>(null);
    useEffect(() => {
        const controller = new AbortController(); scope.current = controller;
        setLoading(true); setCatalog(null); setMaps([]); setError(""); setNotice(""); setBusy(false);
        setOpen(false); setEditing(null); setDraft(empty()); setImage(null); setRemoveImage(false); setSearch(""); setPartyOnly(false);
        void Promise.all([getNpcs(campaignId, controller.signal), getMaps(campaignId, controller.signal)]).then(([npcs, locations]) => {
            if (!controller.signal.aborted) { setCatalog(npcs.data); setMaps(locations.data); }
        }).catch(err => { if (!controller.signal.aborted) setError(extractApiError(err, "Unable to load NPCs. Check campaign access and reload.")); })
            .finally(() => { if (!controller.signal.aborted) setLoading(false); });
        return () => controller.abort();
    }, [campaignId]);
    function reset() { setOpen(false); setEditing(null); setDraft(empty()); setImage(null); setRemoveImage(false); setFormKey(x => x + 1); }
    async function save(event: FormEvent) {
        event.preventDefault();
        if (busy || !catalog?.canManage) return;
        if (image && image.size > 10000000) { setError("Choose an image up to 10 MB."); return; }
        const controller = scope.current;
        setBusy(true); setError(""); setNotice("");
        try {
            const response = await saveNpc(campaignId, editing, draft, image, removeImage, controller?.signal);
            if (controller?.signal.aborted) return;
            setCatalog(current => current && ({ ...current, npcs: [...current.npcs.filter(x => x.id !== response.data.id), response.data].sort((a, b) => a.name.localeCompare(b.name)) }));
            reset(); setNotice("NPC saved.");
        } catch (err) { if (!controller?.signal.aborted) setError(extractApiError(err, "Unable to save NPC. Reload before retrying if the connection failed.")); }
        finally { if (!controller?.signal.aborted) setBusy(false); }
    }
    async function remove(npc: CampaignNpc) {
        if (busy || !catalog?.canManage || !confirm(`Delete ${npc.name} from this campaign?`)) return;
        const controller = scope.current;
        setBusy(true); setError(""); setNotice("");
        try {
            await deleteNpc(campaignId, npc.id, controller?.signal);
            if (controller?.signal.aborted) return;
            setCatalog(current => current && ({ ...current, npcs: current.npcs.filter(x => x.id !== npc.id) }));
            if (editing?.id === npc.id) reset();
            setNotice("NPC deleted.");
        } catch (err) { if (!controller?.signal.aborted) setError(extractApiError(err, "Unable to delete NPC.")); }
        finally { if (!controller?.signal.aborted) setBusy(false); }
    }
    const selectedMap = maps.find(x => x.id === draft.mapId);
    const filtered = catalog?.npcs.filter(x => (!partyOnly || x.isPartyMember) &&
        `${x.name} ${x.description} ${x.locationName}`.toLowerCase().includes(search.toLowerCase())) ?? [];
    return <main className="mx-auto max-w-5xl space-y-5 p-6">
        <Link to="/dashboard" className="text-red-800 underline">Back to campaigns</Link>
        <h1 className="text-3xl font-bold text-stone-900">Campaign NPCs</h1>
        <p className="text-stone-600">NPCs can accompany the party as companions. Player characters are listed separately on the campaign.</p>
        {error && <p role="alert" className="text-red-800">{error}</p>}
        {notice && <p role="status" className="text-green-800">{notice}</p>}
        {loading ? <p>Loading NPCs…</p> : catalog && <>
            {catalog.canManage && <Button disabled={busy} onClick={() => { reset(); setOpen(true); }}>Add NPC</Button>}
            {catalog.canManage && open && <form key={formKey} onSubmit={save} className="space-y-4 rounded-xl border border-stone-300 bg-amber-50 p-5">
                <h2 className="text-xl font-semibold">{editing ? "Edit NPC" : "New NPC"}</h2>
                <fieldset disabled={busy} className="space-y-3">
                    <label className="block">Name<input className={field} required maxLength={120} value={draft.name} onChange={e => setDraft({ ...draft, name: e.target.value })} /></label>
                    <label className="block">Description<textarea className={field} required maxLength={4000} rows={4} value={draft.description} onChange={e => setDraft({ ...draft, description: e.target.value })} /></label>
                    <label className="block">Map<select className={field} required value={draft.mapId ?? ""} onChange={e => setDraft({ ...draft, mapId: e.target.value ? Number(e.target.value) : null, locationName: "" })}>
                        <option value="">Choose a map</option>{maps.map(map => <option key={map.id} value={map.id}>{map.title}</option>)}
                    </select></label>
                    <label className="block">Location<select className={field} required value={draft.locationName} onChange={e => setDraft({ ...draft, locationName: e.target.value })}>
                        <option value="">Choose a location</option>{selectedMap?.locations.map((location, i) => <option key={i} value={location.name}>{location.name}</option>)}
                    </select></label>
                    {maps.length === 0 && <p>Add a <Link className="text-red-800 underline" to={`/campaigns/${campaignId}/maps`}>map with locations</Link> before creating an NPC.</p>}
                    <label className="flex items-center gap-2"><input type="checkbox" checked={draft.isPartyMember} onChange={e => setDraft({ ...draft, isPartyMember: e.target.checked })} />Accompanies the party as an NPC companion</label>
                    <label className="block">Portrait (optional, PNG/JPEG/WebP, up to 10 MB)<input className={field} type="file" accept="image/png,image/jpeg,image/webp" onChange={e => { setImage(e.target.files?.[0] ?? null); setRemoveImage(false); }} /></label>
                    {editing?.hasImage && <><Portrait npc={editing} /><label className="flex items-center gap-2"><input type="checkbox" disabled={!!image} checked={removeImage} onChange={e => setRemoveImage(e.target.checked)} />Remove current portrait</label></>}
                </fieldset>
                <div className="flex gap-2"><Button type="submit" disabled={busy || !draft.name.trim() || !draft.description.trim() || !selectedMap?.locations.some(x => x.name === draft.locationName)}>{busy ? "Saving…" : "Save NPC"}</Button>
                    <Button type="button" variant="secondary" disabled={busy} onClick={reset}>Cancel</Button></div>
            </form>}
            <label className="block">Search NPCs<input className={field} value={search} onChange={e => setSearch(e.target.value)} /></label>
            <label className="flex items-center gap-2"><input type="checkbox" checked={partyOnly} onChange={e => setPartyOnly(e.target.checked)} />NPC companions in the party only</label>
            {filtered.length === 0 ? <p>{catalog.npcs.length === 0 ? "No NPCs yet. The DM can add the first NPC." : "No matching NPCs."}</p> :
                <ul className="grid gap-4 sm:grid-cols-2">{filtered.map(npc => {
                    const map = maps.find(x => x.id === npc.mapId);
                    const validLocation = map?.locations.some(x => x.name === npc.locationName);
                    return <li key={npc.id} className="space-y-3 rounded-xl border border-stone-300 bg-amber-50 p-5">
                        <Portrait npc={npc} />
                        <h2 className="break-words text-xl font-semibold">{npc.name}</h2>
                        {npc.isPartyMember && <span className="inline-block rounded-full bg-emerald-100 px-3 py-1 text-sm text-emerald-900">NPC companion · In the party</span>}
                        <p className="whitespace-pre-wrap break-words">{npc.description}</p>
                        <p className="text-sm">Location: {npc.locationName}{map ? ` · ${map.title}` : ""}{!validLocation && " (location needs updating)"}</p>
                        {catalog.canManage && <div className="flex gap-2"><Button variant="secondary" disabled={busy} onClick={() => {
                            reset(); setEditing(npc); setDraft({ name: npc.name, description: npc.description, mapId: npc.mapId, locationName: npc.locationName, isPartyMember: npc.isPartyMember }); setOpen(true);
                        }}>Edit</Button><Button variant="danger" disabled={busy} onClick={() => void remove(npc)}>Delete</Button></div>}
                    </li>;
                })}</ul>}
        </>}
    </main>;
}

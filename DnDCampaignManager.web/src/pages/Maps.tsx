import { useEffect, useState, type FormEvent } from "react";
import { Link, useParams } from "react-router-dom";
import { useAuth } from "../auth/AuthContext";
import { getCampaigns, type CampaignSummary } from "../api/campaignApi";
import { getMaps, getMapImage, saveMap, deleteMap, indexMap, type CampaignMap, type MapDraft, type MapLocation } from "../api/mapsApi";
import Button from "../components/UI/Button";
import { extractApiError } from "../Utils/apiError";

const newLocation = (): MapLocation => ({ name: "", description: "", x: 50, y: 50 });
const emptyDraft = (): MapDraft => ({ title: "", description: "", locations: [newLocation()] });
const field = "w-full rounded border border-stone-300 bg-white p-2";

function MapImage({ campaignId, map, file, locations, onPin, thumbnail = false }: {
    campaignId: number; map: CampaignMap | null; file?: File | null; locations: MapLocation[];
    onPin?: (x: number, y: number) => void; thumbnail?: boolean;
}) {
    const [url, setUrl] = useState("");
    const [error, setError] = useState("");
    useEffect(() => {
        let active = true;
        let objectUrl = "";
        void Promise.resolve().then(async () => {
            if (!active) return;
            setUrl(""); setError("");
            const blob = file ?? (map ? (await getMapImage(campaignId, map.id)).data : null);
            if (!active || !blob) return;
            objectUrl = URL.createObjectURL(blob); setUrl(objectUrl);
        }).catch(() => { if (active) setError("Unable to load map image. Reload to try again."); });
        return () => { active = false; if (objectUrl) URL.revokeObjectURL(objectUrl); };
    }, [campaignId, map, file]);
    if (error) return <p role="alert">{error}</p>;
    if (!url) return <p>{map ? "Loading image…" : "Choose an image to place location pins."}</p>;
    return <div className={thumbnail ? "relative flex h-32 w-full items-center justify-center overflow-hidden rounded-lg bg-stone-100" : "relative inline-block max-w-full"} onClick={event => {
        if (!onPin) return;
        const bounds = event.currentTarget.getBoundingClientRect();
        onPin(Math.max(0, Math.min(100, (event.clientX - bounds.left) / bounds.width * 100)),
            Math.max(0, Math.min(100, (event.clientY - bounds.top) / bounds.height * 100)));
    }}>
        <img src={url} alt={map?.title ?? "Map draft"} className={thumbnail ? "h-full w-full object-contain" : "block max-h-[650px] max-w-full"} onError={() => setError("This image could not be displayed. Choose another image.")} />
        {!thumbnail && locations.map((location, i) => <span key={i} title={location.name || `Location ${i + 1}`}
            style={{ left: `${location.x}%`, top: `${location.y}%`, transform: "translate(-50%, -50%)" }}
            className="pointer-events-none absolute flex h-7 w-7 items-center justify-center rounded-full border-2 border-white bg-red-800 text-sm font-bold text-white shadow">{i + 1}</span>)}
    </div>;
}

export default function Maps() {
    const { campaignId: routeId } = useParams();
    const campaignId = Number(routeId);
    const { user } = useAuth();
    const [campaign, setCampaign] = useState<CampaignSummary | null>(null);
    const [maps, setMaps] = useState<CampaignMap[]>([]);
    const [loading, setLoading] = useState(true);
    const [busy, setBusy] = useState(false);
    const [error, setError] = useState("");
    const [notice, setNotice] = useState("");
    const [editing, setEditing] = useState<CampaignMap | null>(null);
    const [draft, setDraft] = useState<MapDraft>(emptyDraft);
    const [image, setImage] = useState<File | null>(null);
    const [selectedPin, setSelectedPin] = useState<number | null>(0);
    const [locationBeforeEdit, setLocationBeforeEdit] = useState<MapLocation | null>(null);
    const [formKey, setFormKey] = useState(0);
    const [tab, setTab] = useState<"view" | "manage">("view");
    const [editorOpen, setEditorOpen] = useState(false);
    const canManage = user?.role === "DM" && user.id === campaign?.ownerId;
    useEffect(() => {
        let active = true;
        setEditorOpen(false); setTab("view"); setLoading(true); setError(""); setMaps([]); setCampaign(null);
        setEditing(null); setDraft(emptyDraft()); setImage(null); setSelectedPin(0); setLocationBeforeEdit(null); setFormKey(x => x + 1);
        void Promise.all([getCampaigns(), getMaps(campaignId)]).then(([campaigns, response]) => {
            if (active) { setCampaign(campaigns.data.find(x => x.id === campaignId) ?? null); setMaps(response.data); }
        }).catch(() => { if (active) setError("Unable to load maps. Check campaign access and reload."); })
            .finally(() => { if (active) setLoading(false); });
        return () => { active = false; };
    }, [campaignId]);
    function reset() {
        setEditorOpen(false);
        setEditing(null); setDraft(emptyDraft()); setImage(null); setSelectedPin(0); setLocationBeforeEdit(null); setFormKey(x => x + 1);
    }
    function updateLocation(i: number, patch: Partial<MapLocation>) {
        setDraft(current => ({ ...current, locations: current.locations.map((x, index) => index === i ? { ...x, ...patch } : x) }));
    }
    async function save(event: FormEvent) {
        event.preventDefault();
        if (busy || !canManage) return;
        if (selectedPin !== null) { setError("Finish adding or editing the location before saving the map."); return; }
        if (draft.locations.length === 0) { setError("Add at least one location."); return; }
        if (!editing && !image) { setError("Choose a map image."); return; }
        setBusy(true); setError(""); setNotice("");
        try {
            const response = await saveMap(campaignId, editing?.id ?? null, draft, image);
            setMaps(current => [...current.filter(x => x.id !== response.data.id), response.data]);
            reset();
            setNotice(response.data.indexStatus === "ready" ? "Map saved and searchable in campaign chat."
                : "Map saved. Indexing failed; retry indexing below.");
        } catch (err) { setError(extractApiError(err, "Unable to save map. Reload the list before resubmitting if the connection failed.")); }
        finally { setBusy(false); }
    }
    async function act(map: CampaignMap, remove: boolean) {
        if (busy || !canManage || (remove && !confirm(`Delete ${map.title} and its search index?`))) return;
        setBusy(true); setError(""); setNotice("");
        try {
            if (remove) {
                await deleteMap(campaignId, map.id); setMaps(current => current.filter(x => x.id !== map.id));
                if (editing?.id === map.id) reset(); setNotice("Map deleted.");
            } else {
                const response = await indexMap(campaignId, map.id);
                setMaps(current => current.map(x => x.id === map.id ? response.data : x));
                setNotice(response.data.indexStatus === "ready" ? "Map is searchable." : "Indexing failed. Try again later.");
            }
        } catch (err) { setError(extractApiError(err, "Unable to update map.")); }
        finally { setBusy(false); }
    }
    return <main className="mx-auto max-w-6xl space-y-6 p-6">
        <Link to="/dashboard" className="inline-flex rounded-lg bg-stone-200 px-4 py-2 text-sm font-semibold text-stone-800 hover:bg-stone-300">Back to dashboard</Link>
        <h1 className="text-3xl font-bold">{campaign?.name ?? "Campaign"} maps</h1>
        <p>Maps and locations are shared with campaign members. Campaign chat searches the descriptions you write.</p>
        {error && <p role="alert" className="rounded bg-red-100 p-3 text-red-900">{error}</p>}
        {notice && <p role="status" className="rounded bg-emerald-100 p-3">{notice}</p>}
        {loading ? <p>Loading maps…</p> : <>
            <div role="tablist" aria-label="Map tools" className="flex gap-3">
                <Button type="button" role="tab" id="view-maps-tab" aria-controls="maps-panel" aria-selected={tab === "view"} variant={tab === "view" ? "primary" : "secondary"} disabled={busy} onClick={() => setTab("view")}>View maps</Button>
                {canManage && <Button type="button" role="tab" id="manage-maps-tab" aria-controls="maps-panel" aria-selected={tab === "manage"} variant={tab === "manage" ? "primary" : "secondary"} disabled={busy} onClick={() => setTab("manage")}>Manage maps</Button>}
            </div>
            <section id="maps-panel" role="tabpanel" aria-labelledby={tab === "manage" && canManage ? "manage-maps-tab" : "view-maps-tab"} className="space-y-6">
            {canManage && tab === "manage" && !editorOpen && <section aria-label="Manage map list" className="space-y-3">
                <h2 className="text-xl font-semibold">Maps <span className="text-sm font-normal text-stone-600">({maps.length})</span></h2>
                <div className="max-h-[42.5rem] overflow-y-auto rounded-xl border border-stone-300 bg-amber-50 p-3" tabIndex={maps.length >= 6 ? 0 : undefined} aria-label="Map cards">
                    <ul className="grid auto-rows-[20rem] grid-cols-1 gap-4 md:grid-cols-2 xl:grid-cols-3">
                        {maps.map(map => <li key={map.id} className={`flex h-80 min-w-0 flex-col gap-2 overflow-hidden rounded-xl border bg-white p-3 ${editing?.id === map.id ? "border-emerald-400" : "border-stone-200"}`}>
                            <div className="h-32 shrink-0 overflow-hidden rounded-lg bg-stone-100">
                                <MapImage campaignId={campaignId} map={map} locations={[]} thumbnail />
                            </div>
                            <h3 className="shrink-0 truncate font-semibold" title={map.title}>{map.title}</h3>
                            <p className="min-h-0 flex-1 overflow-hidden text-sm text-stone-600">{map.description || "No description."}</p>
                            <p className="shrink-0 text-xs text-stone-500">{map.locations.length} locations · {map.indexStatus === "ready" ? "Searchable" : "Not searchable yet"}</p>
                            <div className="mt-auto flex shrink-0 flex-wrap gap-2">
                                <Button type="button" size="sm" disabled={busy} variant="secondary" onClick={() => {
                                    setEditing(map); setDraft({ title: map.title, description: map.description, locations: map.locations });
                                    setImage(null); setSelectedPin(null); setLocationBeforeEdit(null); setFormKey(x => x + 1);
                                    setEditorOpen(true); setError(""); setNotice("");
                                }}>Edit</Button>
                                <Button type="button" size="sm" disabled={busy} variant="secondary" onClick={() => void act(map, false)}>Retry indexing</Button>
                                <Button type="button" size="sm" disabled={busy} variant="danger" onClick={() => void act(map, true)}>Delete</Button>
                            </div>
                        </li>)}
                        {Array.from({ length: Math.max(1, 6 - maps.length) }, (_, i) => <li key={`vacant-${i}`} className="flex h-80 flex-col items-center justify-center gap-4 rounded-xl border border-dashed border-stone-300 bg-stone-50/60 p-4 text-sm text-stone-500">
                            <span>Available map space</span>
                            {i === 0 && <Button type="button" disabled={busy} onClick={() => {
                                reset(); setEditorOpen(true); setError(""); setNotice("");
                            }}>Add map</Button>}
                        </li>)}
                    </ul>
                </div>
            </section>}
            {canManage && tab === "manage" && editorOpen && <form key={formKey} onSubmit={save} className="rounded-xl border border-stone-300 bg-amber-50 p-5">
                <fieldset disabled={busy} className="space-y-4">
                    <legend className="text-xl font-semibold">{editing ? "Edit map" : "Add map"}</legend>
                    <label className="block">Title<input className={field} required maxLength={160} value={draft.title} onChange={e => setDraft({ ...draft, title: e.target.value })} /></label>
                    <label className="block">Description<textarea className={field} maxLength={4000} value={draft.description} onChange={e => setDraft({ ...draft, description: e.target.value })} /></label>
                    <label className="block">{editing ? "Replace image (optional)" : "Map image"}<input className={field} type="file" accept="image/png,image/jpeg,image/webp" required={!editing} onChange={e => {
                        const file = e.target.files?.[0] ?? null;
                        if (file && (file.size <= 0 || file.size > 50000000 || !["image/png", "image/jpeg", "image/webp"].includes(file.type))) {
                            setError("Choose a PNG, JPEG or WebP image up to 50 MB."); e.target.value = ""; setImage(null); return;
                        }
                        setImage(file); setError("");
                    }} /></label>
                    <p>Add or edit a location to place its pin on the image. Location changes are saved to the campaign when you choose Save map.</p>
                    <MapImage campaignId={campaignId} map={editing} file={image} locations={draft.locations} onPin={selectedPin === null ? undefined : (x, y) => { if (!busy) updateLocation(selectedPin, { x, y }); }} />
                    {draft.locations.map((location, i) => <div key={i} className="space-y-2 rounded border border-stone-300 bg-white p-3">
                        {selectedPin !== i ? <>
                            <h3 className="font-semibold">{i + 1}. {location.name}</h3>
                            <p className="whitespace-pre-wrap">{location.description || "No description."}</p>
                            <p className="text-sm text-stone-600">Pin: X {location.x.toFixed(1)}%, Y {location.y.toFixed(1)}%</p>
                            <Button type="button" disabled={selectedPin !== null} variant="secondary" onClick={() => { setLocationBeforeEdit({ ...location }); setSelectedPin(i); setError(""); }}>Edit location</Button>
                        </> : <>
                        <h3 className="font-semibold">{locationBeforeEdit ? "Edit location" : "New location"} · Pin {i + 1}</h3>
                        <label className="block">Name<input className={field} required maxLength={120} value={location.name} onChange={e => updateLocation(i, { name: e.target.value })} /></label>
                        <label className="block">Location description<textarea className={field} maxLength={4000} value={location.description} onChange={e => updateLocation(i, { description: e.target.value })} /></label>
                        <div className="flex gap-3">{(["x", "y"] as const).map(axis => <label key={axis}>{axis.toUpperCase()} (%)<input className={field} type="number" required min={0} max={100} step="any" value={location[axis]} onChange={e => updateLocation(i, { [axis]: Number(e.target.value) })} /></label>)}</div>
                        <div className="flex gap-4">
                            <Button type="button" variant="secondary" onClick={() => {
                                if (!location.name.trim() || !Number.isFinite(location.x) || !Number.isFinite(location.y) || location.x < 0 || location.x > 100 || location.y < 0 || location.y > 100) {
                                    setError("Enter a location name and coordinates between 0 and 100."); return;
                                }
                                updateLocation(i, { name: location.name.trim(), description: location.description.trim() });
                                setSelectedPin(null); setLocationBeforeEdit(null); setError("");
                            }}>{locationBeforeEdit ? "Save location changes" : "Add location"}</Button>
                            <Button type="button" variant="secondary" onClick={() => {
                                if (locationBeforeEdit) updateLocation(i, locationBeforeEdit);
                                else setDraft({ ...draft, locations: draft.locations.filter((_, index) => i !== index) });
                                setSelectedPin(null); setLocationBeforeEdit(null); setError("");
                            }}>Cancel location</Button>
                        </div>
                        </>}
                        {selectedPin === null && <Button type="button" variant="danger" onClick={() => {
                            setDraft({ ...draft, locations: draft.locations.filter((_, index) => i !== index) });
                        }}>Remove location</Button>}
                    </div>)}
                    <div className="flex flex-wrap gap-4">
                        <Button type="button" disabled={draft.locations.length >= 50 || selectedPin !== null} variant="secondary" onClick={() => { setDraft({ ...draft, locations: [...draft.locations, newLocation()] }); setLocationBeforeEdit(null); setSelectedPin(draft.locations.length); }}>New location</Button>
                        <Button type="submit">{busy ? "Saving…" : "Save map"}</Button>
                        <Button type="button" variant="secondary" onClick={() => { reset(); setError(""); setNotice(""); }}>{editing ? "Cancel edit" : "Cancel add"}</Button>
                    </div>
                </fieldset>
            </form>}
            {maps.length === 0 && tab === "view" && <p>No maps yet.{canManage ? " Add your first map in Manage maps." : " Your DM can add maps here."}</p>}
            {tab === "view" && maps.map(map => <article key={map.id} className="space-y-3 rounded-xl border border-stone-300 bg-amber-50 p-5">
                <h2 className="text-xl font-semibold">{map.title}</h2>
                <p className="whitespace-pre-wrap">{map.description}</p>
                <MapImage campaignId={campaignId} map={map} locations={map.locations} />
                <ol className="list-inside list-decimal space-y-2">{map.locations.map((location, i) => <li key={i}><strong>{location.name}</strong><p className="whitespace-pre-wrap">{location.description}</p></li>)}</ol>
            </article>)}
            </section>
        </>}
    </main>;
}

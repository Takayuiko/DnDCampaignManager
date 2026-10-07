import { useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { createItem, deleteItem, deleteAllItems, previewDeleteAllItems, getItems, importSrdItems, updateItem, type DeleteItemsPreview, type Item, type ItemCatalog, type ItemInput } from "../api/itemApi";
import Button from "../components/UI/Button";
import ErrorPanel from "../components/UI/ErrorPanel";
import SrdAttribution from "../components/SrdAttribution";
import { extractApiError } from "../Utils/apiError";

const empty: ItemInput = { name: "", category: "Adventuring gear", description: "", weightLb: null, costGp: null };
const inputClass = "w-full rounded border border-stone-300 bg-white p-2";

export default function Items() {
    const campaignId = Number(useParams().campaignId);
    const [catalog, setCatalog] = useState<ItemCatalog | null>(null);
    const [form, setForm] = useState<ItemInput>(empty);
    const [editing, setEditing] = useState<number | null>(null);
    const [search, setSearch] = useState("");
    const [loading, setLoading] = useState(true);
    const [busy, setBusy] = useState(false);
    const [error, setError] = useState<string | null>(null);
    const [success, setSuccess] = useState<string | null>(null);
    const [deletionPreview, setDeletionPreview] = useState<DeleteItemsPreview | null>(null);

    useEffect(() => {
        let active = true;
        setLoading(true);
        setCatalog(null);
        setError(null);
        setDeletionPreview(null);
        const load = async () => {
            try {
                if (!Number.isInteger(campaignId) || campaignId <= 0) throw new Error("Invalid campaign id.");
                const response = await getItems(campaignId);
                if (active) setCatalog(response.data);
            } catch (err: unknown) {
                if (active) setError(extractApiError(err, "Unable to load items."));
            } finally { if (active) setLoading(false); }
        };
        void load();
        return () => { active = false; };
    }, [campaignId]);

    const run = async (operation: () => Promise<string>) => {
        setBusy(true); setError(null); setSuccess(null);
        try {
            const message = await operation();
            setSuccess(message);
            setCatalog((await getItems(campaignId)).data);
        } catch (err: unknown) { setError(extractApiError(err, "Unable to update items.")); }
        finally { setBusy(false); }
    };
    const save = (event: React.FormEvent) => {
        event.preventDefault();
        void run(async () => {
            const request = { ...form, name: form.name.trim(), category: form.category.trim() };
            if (editing === null) await createItem(campaignId, request);
            else await updateItem(campaignId, editing, request);
            setEditing(null); setForm(empty);
            return "Item saved.";
        });
    };
    const edit = (item: Item) => { setEditing(item.id); setForm(item); setSuccess(null); };
    const filtered = catalog?.items.filter(item => `${item.name} ${item.category}`.toLowerCase().includes(search.toLowerCase())) ?? [];

    return <main className="min-h-screen bg-stone-100 px-4 py-8">
        <div className="mx-auto max-w-5xl space-y-5 rounded-xl border border-stone-300 bg-amber-50 p-6 shadow-lg">
            <div className="flex flex-wrap items-center justify-between gap-3">
                <div><h1 className="text-3xl font-bold text-stone-800">Campaign Items</h1>
                    <p className="text-sm text-stone-600">Build the catalog used by this campaign's character inventories.</p></div>
                <Link to="/dashboard" className="underline">Back to dashboard</Link>
            </div>
            {error && <ErrorPanel message={error} />}
            {success && <p role="status" className="text-emerald-800">{success}</p>}
            {loading ? <p>Loading items...</p> : catalog && <>
                {catalog.canManage && <>
                    <div className="flex flex-wrap items-center gap-3">
                        <Button disabled={busy} onClick={() => void run(async () => {
                            const result = await importSrdItems(campaignId);
                            return `${result.data.added} starter items added; ${result.data.updated} updated with missing SRD values. Existing values were kept.`;
                        })}>Import 5e SRD starter items</Button>
                        <Button type="button" variant="danger" className="sm:ml-auto" disabled={busy || catalog.items.length === 0 || deletionPreview !== null}
                            onClick={async () => {
                                setBusy(true); setError(null); setSuccess(null);
                                try { setDeletionPreview((await previewDeleteAllItems(campaignId)).data); }
                                catch (err: unknown) { setError(extractApiError(err, "Unable to review item deletion.")); }
                                finally { setBusy(false); }
                            }}>Delete all items</Button>
                    </div>
                    <p className="text-sm text-stone-600">Includes prices in gp and weights in lb. Reimport to fill missing values on SRD items.</p>
                    {deletionPreview && <section role="alertdialog" aria-labelledby="delete-items-title" aria-describedby="delete-items-description"
                        className="space-y-3 rounded-lg border border-red-300 bg-red-50 p-4">
                        <h2 id="delete-items-title" className="font-semibold text-red-900">Delete all campaign items?</h2>
                        <p id="delete-items-description" className="text-sm text-red-900">
                            This permanently deletes {deletionPreview.itemCount} catalog items and {deletionPreview.inventoryEntryCount} inventory entries
                            from all characters in this campaign. This cannot be undone.
                        </p>
                        <div className="flex flex-wrap gap-2">
                            <Button type="button" variant="secondary" disabled={busy} onClick={() => setDeletionPreview(null)}>Cancel deletion</Button>
                            <Button type="button" variant="danger" disabled={busy} onClick={() => void run(async () => {
                                const preview = deletionPreview;
                                setDeletionPreview(null);
                                const result = await deleteAllItems(campaignId, preview);
                                setEditing(null); setForm(empty); setSearch("");
                                return `Deleted ${result.data.itemCount} catalog items and ${result.data.inventoryEntryCount} inventory entries.`;
                            })}>{busy ? "Deleting..." : "Delete all items permanently"}</Button>
                        </div>
                    </section>}
                    <form onSubmit={save} className="space-y-3 rounded-lg border border-stone-300 bg-stone-50 p-4">
                        <h2 className="text-lg font-semibold">{editing === null ? "Create item" : "Edit item"}</h2>
                        <fieldset disabled={busy} className="grid gap-3 sm:grid-cols-2">
                            <label>Name<input className={inputClass} required maxLength={120} value={form.name} onChange={e => setForm({ ...form, name: e.target.value })} /></label>
                            <label>Category<input className={inputClass} required maxLength={50} value={form.category} onChange={e => setForm({ ...form, category: e.target.value })} /></label>
                            <label>Weight (lb, optional)<input className={inputClass} type="number" min="0" max="1000000" step="0.0001" value={form.weightLb ?? ""} onChange={e => setForm({ ...form, weightLb: e.target.value === "" ? null : Number(e.target.value) })} /></label>
                            <label>Cost (gp, optional)<input className={inputClass} type="number" min="0" max="1000000000" step="0.0001" value={form.costGp ?? ""} onChange={e => setForm({ ...form, costGp: e.target.value === "" ? null : Number(e.target.value) })} /></label>
                            <label className="sm:col-span-2">Description<textarea className={inputClass} rows={3} maxLength={4000} value={form.description} onChange={e => setForm({ ...form, description: e.target.value })} /></label>
                        </fieldset>
                        <div className="flex gap-2"><Button type="submit" disabled={busy || !form.name.trim() || !form.category.trim()}>{busy ? "Working..." : "Save item"}</Button>
                            {editing !== null && <Button variant="subtle" disabled={busy} onClick={() => { setEditing(null); setForm(empty); }}>Cancel edit</Button>}</div>
                        <p className="text-xs text-stone-600">Edits update this item in all assigned inventories. Assigned items cannot be deleted.</p>
                    </form>
                </>}
                <label className="block">Search items<input className={inputClass} value={search} onChange={e => setSearch(e.target.value)} placeholder="Name or category" /></label>
                <p className="text-sm text-stone-600">{filtered.length} of {catalog.items.length} items</p>
                {filtered.length === 0 ? <p>{catalog.items.length === 0 ? "No items yet. The DM can create an item or import starter equipment." : "No matching items."}</p> :
                    <ul className="space-y-2">{filtered.map(item => <li key={item.id} className="rounded-lg border border-stone-200 bg-white p-4">
                        <div className="flex flex-wrap justify-between gap-3"><div>
                            <h3 className="font-semibold">{item.name}</h3><p className="text-sm text-stone-600">{item.category}{item.source && ` · ${item.source}`}</p>
                            <p className="text-sm">Weight: {item.weightLb === null ? "Unspecified" : `${item.weightLb} lb`} · Cost: {item.costGp === null ? "Unspecified" : `${item.costGp} gp`}</p>
                        </div>{catalog.canManage && <div className="flex gap-2 self-start">
                            <Button size="sm" disabled={busy} variant="secondary" onClick={() => edit(item)}>Edit</Button>
                            <Button size="sm" disabled={busy} variant="danger" onClick={() => {
                                if (confirm(`Delete ${item.name} from the campaign catalog?`)) void run(async () => {
                                    await deleteItem(campaignId, item.id);
                                    if (editing === item.id) { setEditing(null); setForm(empty); }
                                    return "Item deleted.";
                                });
                            }}>Delete</Button>
                        </div>}</div>
                        {item.description && <p className="mt-2 whitespace-pre-wrap text-sm">{item.description}</p>}
                    </li>)}</ul>}
                <SrdAttribution />
            </>}
        </div>
    </main>;
}

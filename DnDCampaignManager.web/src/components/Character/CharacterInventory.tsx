import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { assignItem, getInventory, getItems, type CharacterInventory as Inventory, type Item } from "../../api/itemApi";
import { extractApiError } from "../../Utils/apiError";
import Button from "../UI/Button";
import ErrorPanel from "../UI/ErrorPanel";
import SrdAttribution from "../SrdAttribution";

export default function CharacterInventory({ campaignId, characterId, readOnly = false }: {
    campaignId: number; characterId: number; readOnly?: boolean;
}) {
    const [inventory, setInventory] = useState<Inventory | null>(null);
    const [catalog, setCatalog] = useState<Item[]>([]);
    const [itemId, setItemId] = useState("");
    const [quantity, setQuantity] = useState(1);
    const [notes, setNotes] = useState("");
    const [loading, setLoading] = useState(true);
    const [busy, setBusy] = useState(false);
    const [error, setError] = useState<string | null>(null);
    const [success, setSuccess] = useState<string | null>(null);

    useEffect(() => {
        let active = true;
        const controller = new AbortController();
        setLoading(true); setInventory(null); setCatalog([]); setError(null);
        const load = async () => {
            try {
                const response = await getInventory(campaignId, characterId, controller.signal);
                const items = response.data.canAssign ? (await getItems(campaignId, controller.signal)).data.items : [];
                if (active) { setInventory(response.data); setCatalog(items); }
            } catch (err: unknown) { if (active) setError(extractApiError(err, "Unable to load inventory.")); }
            finally { if (active) setLoading(false); }
        };
        void load();
        return () => { active = false; controller.abort(); };
    }, [campaignId, characterId]);

    const assign = async (event: React.FormEvent) => {
        event.preventDefault();
        setBusy(true); setError(null); setSuccess(null);
        try {
            const response = await assignItem(campaignId, characterId, { itemId: Number(itemId), quantity, notes });
            setInventory(current => current && { ...current, items: [...current.items, response.data] });
            setSuccess(`${response.data.item.name} assigned.`); setItemId(""); setQuantity(1); setNotes("");
        } catch (err: unknown) { setError(extractApiError(err, "Unable to assign item.")); }
        finally { setBusy(false); }
    };

    return <section className="space-y-4 rounded-xl border border-stone-300 bg-stone-50 p-4">
        <h2 className="text-2xl font-bold">Inventory</h2>
        {error && <ErrorPanel message={error} />}
        {!readOnly && success && <p role="status" className="text-emerald-800">{success}</p>}
        {loading ? <p>Loading inventory...</p> : inventory && <>
            {!readOnly && inventory.canAssign && <>
                <Link className="inline-block underline" to={`/campaigns/${campaignId}/items`}>Manage campaign items</Link>
                {catalog.length === 0 ? <p>Create or import campaign items before assigning them.</p> :
                    <form onSubmit={assign} className="space-y-3 rounded-lg border border-stone-200 bg-stone-50 p-4">
                        <fieldset disabled={busy} className="grid gap-3 sm:grid-cols-3">
                            <label className="sm:col-span-2">Item<select required className="w-full rounded border p-2" value={itemId} onChange={e => setItemId(e.target.value)}>
                                <option value="">Choose an item</option>{catalog.map(item => <option key={item.id} value={item.id}>{item.name} ({item.category})</option>)}
                            </select></label>
                            <label>Quantity<input required type="number" min="1" max="2147483647" step="1" className="w-full rounded border p-2" value={quantity} onChange={e => setQuantity(Number(e.target.value))} /></label>
                            <label className="sm:col-span-3">Inventory notes<textarea className="w-full rounded border p-2" rows={2} maxLength={2000} value={notes} onChange={e => setNotes(e.target.value)} /></label>
                        </fieldset>
                        <Button type="submit" disabled={busy || !itemId || !Number.isInteger(quantity) || quantity < 1}>{busy ? "Assigning..." : "Assign item"}</Button>
                    </form>}
            </>}
            {inventory.items.length === 0 ? <p>No items assigned yet.</p> : <ul className="space-y-2">
                {inventory.items.map(entry => <li key={entry.id} className="rounded-lg border border-stone-200 bg-white p-3">
                    <h3 className="font-semibold">{entry.item.name} × {entry.quantity}</h3>
                    <p className="text-sm text-stone-600">{entry.item.category}</p>
                    {entry.item.description && <p className="whitespace-pre-wrap text-sm">{entry.item.description}</p>}
                    <p className="text-sm">Weight each: {entry.item.weightLb === null ? "Unspecified" : `${entry.item.weightLb} lb`} · Cost each: {entry.item.costGp === null ? "Unspecified" : `${entry.item.costGp} gp`}</p>
                    {entry.notes && <p className="mt-2 whitespace-pre-wrap text-sm">Notes: {entry.notes}</p>}
                </li>)}
            </ul>}
            {inventory.items.some(entry => entry.item.source) && <SrdAttribution />}
        </>}
    </section>;
}

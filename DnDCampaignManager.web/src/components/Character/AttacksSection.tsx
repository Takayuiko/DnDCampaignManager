import type { Dispatch, SetStateAction } from "react";
import type { AttackDraft, CharacterSectionProps } from "./characterTypes";
import { formatModUtil } from "../../Utils/dnd";
import { sectionClass } from "./characterSectionStyles";

type Props = CharacterSectionProps & {
    draft: AttackDraft | null;
    setDraft: Dispatch<SetStateAction<AttackDraft | null>>;
    submitting?: boolean;
};

export default function AttacksSection({ form, setForm, draft, setDraft, submitting }: Props) {
    function confirmDraft() {
        if (!draft || !draft.name.trim() || draft.name.length > 120 || draft.damage.length > 500 ||
            !Number.isInteger(draft.attackBonus) || Math.abs(draft.attackBonus) > 1000 || submitting ||
            (!draft.clientId && form.attacks.length >= 50)) return;
        const attack = { ...draft, name: draft.name.trim(), damage: draft.damage.trim(), clientId: draft.clientId ?? crypto.randomUUID() };
        setForm(previous => ({ ...previous, attacks: draft.clientId
            ? previous.attacks.map(current => current.clientId === draft.clientId ? attack : current)
            : [...previous.attacks, attack] }));
        setDraft(null);
    }

    return <section className={sectionClass}>
        <div className="flex items-center justify-between mb-4">
            <h2 className="text-lg font-semibold text-stone-700">Attacks & Spellcasting</h2>
            <button type="button" disabled={!!draft || submitting || form.attacks.length >= 50}
                onClick={() => setDraft({ id: null, name: "", attackBonus: 0, damage: "" })}
                className="px-3 py-1.5 rounded-md bg-stone-900 text-amber-50 text-sm font-semibold hover:bg-stone-800 disabled:opacity-50">
                New attack
            </button>
            {form.attacks.length >= 50 && <p className="text-xs text-stone-600">Maximum of 50 attacks reached.</p>}
        </div>

        {draft && <div className="mb-4 space-y-3 rounded-lg border border-stone-300 bg-amber-50 p-3" aria-label="Attack editor">
            <h3 className="font-semibold text-stone-800">{draft.clientId ? "Modify attack" : "New attack"}</h3>
            <div className="grid grid-cols-1 gap-3 md:grid-cols-3">
                <label className="block text-xs font-semibold text-stone-600">Name
                    <input maxLength={120} value={draft.name} disabled={submitting} onChange={event => {
                        const name = event.target.value;
                        setDraft(previous => previous ? { ...previous, name } : previous);
                    }} className="mt-1 w-full border border-stone-300 rounded-md p-2" placeholder="Longsword" />
                </label>
                <label className="block text-xs font-semibold text-stone-600">Atk Bonus
                    <input type="number" min={-1000} max={1000} value={draft.attackBonus} disabled={submitting} onChange={event => {
                        const attackBonus = Number(event.target.value);
                        setDraft(previous => previous ? { ...previous, attackBonus } : previous);
                    }} className="mt-1 w-full border border-stone-300 rounded-md p-2" />
                </label>
                <label className="block text-xs font-semibold text-stone-600">Damage/Type
                    <input maxLength={500} value={draft.damage} disabled={submitting} onChange={event => {
                        const damage = event.target.value;
                        setDraft(previous => previous ? { ...previous, damage } : previous);
                    }} className="mt-1 w-full border border-stone-300 rounded-md p-2" placeholder="1d8+3 slashing" />
                </label>
            </div>
            <div className="flex justify-end gap-2">
                <button type="button" onClick={() => setDraft(null)} disabled={submitting}
                    className="px-3 py-2 text-sm rounded-md border border-stone-300 bg-white">Cancel attack</button>
                <button type="button" onClick={confirmDraft} disabled={submitting || !draft.name.trim() || !Number.isInteger(draft.attackBonus) || Math.abs(draft.attackBonus) > 1000 || draft.name.length > 120 || draft.damage.length > 500}
                    className="px-3 py-2 text-sm font-semibold rounded-md bg-emerald-700 text-white hover:bg-emerald-600 disabled:opacity-50">
                    {draft.clientId ? "Save attack changes" : "Add attack"}
                </button>
            </div>
            <p className="text-xs text-stone-600">Add or cancel attack changes before saving the character.</p>
        </div>}

        {form.attacks.length === 0 ? <p className="text-stone-600 text-sm">No attacks yet.</p> :
            <ul className="space-y-3">
                {form.attacks.map(attack => <li key={attack.clientId} className="flex flex-wrap items-center justify-between gap-3 rounded-md border border-stone-200 bg-white p-3">
                    <div>
                        <h3 className="font-semibold text-stone-800">{attack.name || "Unnamed attack"}</h3>
                        <p className="text-sm text-stone-600">Attack bonus {formatModUtil(attack.attackBonus)} · {attack.damage || "No damage text"}</p>
                    </div>
                    <div className="flex gap-2">
                        <button type="button" disabled={!!draft || submitting} onClick={() => setDraft({ ...attack })}
                            className="px-3 py-1.5 rounded-md border border-stone-300 bg-stone-100 hover:bg-stone-200 text-stone-700 text-sm disabled:opacity-50">Modify</button>
                        <button type="button" disabled={!!draft || submitting} onClick={() => {
                            if (!confirm("Remove this attack?")) return;
                            setForm(previous => ({ ...previous, attacks: previous.attacks.filter(current => current.clientId !== attack.clientId) }));
                        }} className="px-3 py-1.5 rounded-md border border-stone-300 bg-stone-100 hover:bg-stone-200 text-stone-700 text-sm disabled:opacity-50">Remove</button>
                    </div>
                </li>)}
            </ul>}
        <p className="mt-2 text-xs text-stone-600">Use this for weapon attacks or spell attacks (e.g., Fire Bolt +5, 1d10 fire). Save the character to persist attack changes.</p>
    </section>;
}

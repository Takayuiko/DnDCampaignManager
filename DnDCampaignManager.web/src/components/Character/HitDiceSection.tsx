import type { CharacterSectionProps } from "./characterTypes";
import { sectionClass, sectionTitleClass } from "./characterSectionStyles";

export default function HitDiceSection({ form, setForm }: CharacterSectionProps) {
    return (<>
        {/* Hit Dice */}
        <section className={sectionClass}>
            <h2 className={sectionTitleClass}>Hit Dice</h2>

            <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
                <div>
                    <label className="block text-sm font-semibold text-stone-700">Die</label>
                    <input
                        maxLength={16}
                        required={form.hitDice.total > 0}
                        value={form.hitDice.die}
                        onChange={(e) =>
                            setForm((prev) => ({
                                ...prev,
                                hitDice: { ...prev.hitDice, die: e.target.value },
                            }))
                        }
                        className="w-full border border-stone-400 rounded-md p-2 bg-white"
                        placeholder="d8"
                    />
                </div>

                <div>
                    <label className="block text-sm font-semibold text-stone-700">Total</label>
                    <input
                        type="number"
                        min={0}
                        max={1000}
                        value={form.hitDice.total}
                        onChange={(e) => {
                            const v = Number(e.target.value);
                            setForm((prev) => ({
                                ...prev,
                                hitDice: {
                                    ...prev.hitDice,
                                    total: v,
                                    remaining: Math.min(prev.hitDice.remaining, v),
                                },
                            }));
                        }}
                        className="w-full border border-stone-400 rounded-md p-2 bg-white"
                    />
                </div>

                <div>
                    <label className="block text-sm font-semibold text-stone-700">Remaining</label>
                    <input
                        type="number"
                        min={0}
                        max={form.hitDice.total}
                        value={form.hitDice.remaining}
                        onChange={(e) => {
                            const v = Number(e.target.value);
                            setForm((prev) => ({
                                ...prev,
                                hitDice: {
                                    ...prev.hitDice,
                                    remaining: Math.max(0, Math.min(v, prev.hitDice.total)),
                                },
                            }));
                        }}
                        className="w-full border border-stone-400 rounded-md p-2 bg-white"
                    />
                </div>
            </div>

            <p className="mt-2 text-xs text-stone-600">
                Spend hit dice during short rests. Total is usually your level.
            </p>
        </section>
    </>);
}

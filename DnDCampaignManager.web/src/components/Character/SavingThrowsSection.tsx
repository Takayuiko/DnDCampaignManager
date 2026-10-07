import type { CharacterSectionProps } from "./characterTypes";
import { SAVING_THROW_ROWS } from "./characterConstants";
import { characterCalculations } from "./characterCalculations";

export default function SavingThrowsSection({ form, setForm }: CharacterSectionProps) {
    const { formatMod, savingThrowTotal } = characterCalculations(form);
    return (<>
        {/* Saving throws */}
        {SAVING_THROW_ROWS.map(({ key, label }) => {
            const total = savingThrowTotal(key, form);
            const st = form.savingThrows[key] ?? { isProficient: false, miscBonus: 0 };

            return (
                <div
                    key={key}
                    className="flex items-center justify-between border border-stone-200 rounded-md bg-white px-3 py-2"
                >
                    <div className="min-w-0">
                        <div className="font-semibold text-stone-800">{label}</div>
                        <div className="text-xs text-stone-500">Saving Throw</div>
                    </div>

                    <div className="flex items-center gap-3">
                        <div className="w-10 text-center font-bold text-stone-800">
                            {formatMod(total)}
                        </div>

                        <label className="text-xs text-stone-600 flex items-center gap-1">
                            Prof
                            <input
                                type="checkbox"
                                checked={!!st.isProficient}
                                onChange={(e) =>
                                    setForm((prev) => ({
                                        ...prev,
                                        savingThrows: {
                                            ...prev.savingThrows,
                                            [key]: {
                                                ...prev.savingThrows[key],
                                                isProficient: e.target.checked,
                                            },
                                        },
                                    }))
                                }
                            />
                        </label>

                        <input
                            type="number"
                            min={-1000}
                            max={1000}
                            value={st.miscBonus ?? 0}
                            onChange={(e) =>
                                setForm((prev) => ({
                                    ...prev,
                                    savingThrows: {
                                        ...prev.savingThrows,
                                        [key]: {
                                            ...prev.savingThrows[key],
                                            miscBonus: Number(e.target.value),
                                        },
                                    },
                                }))
                            }
                            className="w-16 text-center border border-stone-300 rounded p-1 text-sm"
                            title="Misc bonus"
                        />
                    </div>
                </div>
            );
        })}
    </>);
}

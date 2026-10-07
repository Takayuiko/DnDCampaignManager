import type { CharacterSectionProps } from "./characterTypes";
import { sectionClass, sectionTitleClass } from "./characterSectionStyles";
import { characterCalculations } from "./characterCalculations";

export default function CoreSection({ form, setForm }: CharacterSectionProps) {
    const { proficiencyFromLevel, formatMod, passivePerception } = characterCalculations(form);
    return (<>
        {/* Core */}
        <section className={sectionClass}>
            <h2 className={sectionTitleClass}>Core</h2>

            <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
                <div className="flex items-center justify-between border border-stone-200 rounded-md bg-white px-3 py-2">
                    <div>
                        <div className="font-semibold text-stone-800">Inspiration</div>
                        <div className="text-xs text-stone-500">Toggle when earned</div>
                    </div>
                    <input
                        type="checkbox"
                        checked={!!form.inspiration}
                        onChange={(e) =>
                            setForm((prev) => ({ ...prev, inspiration: e.target.checked }))
                        }
                        className="h-5 w-5"
                    />
                </div>

                <div className="flex items-center justify-between border border-stone-200 rounded-md bg-white px-3 py-2">
                    <div>
                        <div className="font-semibold text-stone-800">Proficiency Bonus</div>
                        <div className="text-xs text-stone-500">From level</div>
                    </div>
                    <div className="font-bold text-stone-800">
                        {formatMod(proficiencyFromLevel(form.level))}
                    </div>
                </div>

                <div className="flex items-center justify-between border border-stone-200 rounded-md bg-white px-3 py-2">
                    <div>
                        <div className="font-semibold text-stone-800">Passive Perception</div>
                        <div className="text-xs text-stone-500">10 + Perception mod</div>
                    </div>
                    <div className="font-bold text-stone-800">{passivePerception(form)}</div>
                </div>
            </div>
        </section>
    </>);
}

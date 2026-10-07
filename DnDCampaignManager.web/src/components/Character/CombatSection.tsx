import type { CharacterSectionProps, CharacterFieldChange } from "./characterTypes";
import { sectionClass, sectionTitleClass } from "./characterSectionStyles";

export default function CombatSection({ form, handleChange }: Pick<CharacterSectionProps, "form"> & { handleChange: CharacterFieldChange }) {
    return (<>
        {/* Combat + HP */}
        <section className={sectionClass}>
            <h2 className={sectionTitleClass}>Combat</h2>

            <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
                <div>
                    <label className="block text-sm font-semibold text-stone-700">
                        Armor Class
                    </label>
                    <input
                        type="number"
                        name="armorClass"
                        min={0}
                        value={form.armorClass}
                        onChange={handleChange}
                        className="w-full border border-stone-400 rounded-md p-2 bg-white"
                    />
                </div>

                <div>
                    <label className="block text-sm font-semibold text-stone-700">
                        Initiative
                    </label>
                    <input
                        type="number"
                        name="initiative"
                        value={form.initiative}
                        onChange={handleChange}
                        className="w-full border border-stone-400 rounded-md p-2 bg-white"
                    />
                </div>

                <div>
                    <label className="block text-sm font-semibold text-stone-700">
                        Speed
                    </label>
                    <input
                        type="number"
                        name="speed"
                        min={0}
                        value={form.speed}
                        onChange={handleChange}
                        className="w-full border border-stone-400 rounded-md p-2 bg-white"
                    />
                </div>
            </div>

            <div className="mt-6">
                <h3 className="text-md font-semibold text-stone-700 mb-3">
                    Hit Points
                </h3>

                <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
                    <div>
                        <label className="block text-sm font-semibold text-stone-700">
                            Max HP
                        </label>
                        <input
                            type="number"
                            name="hitPointMax"
                            min={0}
                            value={form.hitPointMax}
                            onChange={handleChange}
                            className="w-full border border-stone-400 rounded-md p-2 bg-white"
                        />
                    </div>

                    <div>
                        <label className="block text-sm font-semibold text-stone-700">
                            Current HP
                        </label>
                        <input
                            type="number"
                            name="hitPointCurrent"
                            min={0}
                            value={form.hitPointCurrent}
                            onChange={handleChange}
                            className="w-full border border-stone-400 rounded-md p-2 bg-white"
                        />
                    </div>

                    <div>
                        <label className="block text-sm font-semibold text-stone-700">
                            Temp HP
                        </label>
                        <input
                            type="number"
                            name="hitPointTemporary"
                            min={0}
                            value={form.hitPointTemporary}
                            onChange={handleChange}
                            className="w-full border border-stone-400 rounded-md p-2 bg-white"
                        />
                    </div>
                </div>
            </div>
        </section>
    </>);
}

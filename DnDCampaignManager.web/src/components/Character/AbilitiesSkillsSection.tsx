import type { CharacterSectionProps, CharacterFieldChange } from "./characterTypes";
import { sectionClass, sectionTitleClass } from "./characterSectionStyles";
import { abilityAbbrev } from "./characterConstants";
import { characterCalculations } from "./characterCalculations";

export default function AbilitiesSkillsSection({ form, setForm, handleChange }: CharacterSectionProps & { handleChange: CharacterFieldChange }) {
    const { prettySkill, proficiencyFromLevel, formatMod, calcSkillMod } = characterCalculations(form);
    return (<>
        {/* Abilities + Skills */}
        <section className="grid grid-cols-1 md:grid-cols-3 gap-6">
            <div className={sectionClass}>
                <h2 className={sectionTitleClass}>Ability Scores</h2>

                {(
                    [
                        ["strength", "STR"],
                        ["dexterity", "DEX"],
                        ["constitution", "CON"],
                        ["intelligence", "INT"],
                        ["wisdom", "WIS"],
                        ["charisma", "CHA"],
                    ] as const
                ).map(([key, label]) => (
                    <div key={key} className="flex items-center justify-between mb-3">
                        <label className="uppercase text-sm font-bold text-stone-600">
                            {label}
                        </label>

                        <input
                            type="number"
                            name={key}
                            min={0}
                            max={100}
                            value={form[key]}
                            onChange={handleChange}
                            className="w-24 text-center border border-stone-400 rounded-md p-1 bg-white"
                        />
                    </div>
                ))}
            </div>

            <div className={`${sectionClass} md:col-span-2`}>
                <div className="flex items-center justify-between mb-4">
                    <h2 className="text-lg font-semibold text-stone-700">Skills</h2>

                    <div className="text-xs text-stone-600">
                        Proficiency Bonus:{" "}
                        <span className="font-bold text-stone-800">
                            {formatMod(proficiencyFromLevel(form.level))}
                        </span>
                    </div>
                </div>

                {(!form.skills || form.skills.length === 0) ? (
                    <p className="text-stone-600 text-sm">No skills loaded yet.</p>
                ) : (
                    <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
                        {form.skills.map((sk, idx) => {
                            const mod = calcSkillMod(sk);

                            return (
                                <div
                                    key={String(sk.skill)}
                                    className="flex items-center justify-between border border-stone-200 rounded-md bg-white px-3 py-2"
                                >
                                    <div className="min-w-0">
                                        <div className="font-semibold text-stone-800 truncate">
                                            {prettySkill(String(sk.skill))}
                                        </div>
                                        <div className="text-xs text-stone-500">
                                            {abilityAbbrev(sk.ability)}
                                        </div>
                                    </div>

                                    <div className="flex items-center gap-3">
                                        <div className="w-10 text-center font-bold text-stone-800">
                                            {formatMod(mod)}
                                        </div>

                                        <label className="text-xs text-stone-600 flex items-center gap-1">
                                            Prof
                                            <input
                                                type="checkbox"
                                                checked={!!sk.isProficient}
                                                onChange={(e) => {
                                                    const checked = e.target.checked;
                                                    setForm((prev) => {
                                                        const copy = [...prev.skills];
                                                        const current = copy[idx];

                                                        copy[idx] = {
                                                            ...current,
                                                            isProficient: checked,
                                                            isExpertise: checked ? current.isExpertise : false,
                                                        };
                                                        return { ...prev, skills: copy };
                                                    });
                                                }}
                                            />
                                        </label>

                                        <label className="text-xs text-stone-600 flex items-center gap-1">
                                            Exp
                                            <input
                                                type="checkbox"
                                                disabled={!sk.isProficient}
                                                checked={!!sk.isExpertise}
                                                onChange={(e) => {
                                                    const checked = e.target.checked;
                                                    setForm((prev) => {
                                                        const copy = [...prev.skills];
                                                        copy[idx] = { ...copy[idx], isExpertise: checked };
                                                        return { ...prev, skills: copy };
                                                    });
                                                }}
                                            />
                                        </label>

                                        <input
                                            type="number"
                                            min={-1000}
                                            max={1000}
                                            value={sk.miscBonus ?? 0}
                                            onChange={(e) => {
                                                const v = Number(e.target.value);
                                                setForm((prev) => {
                                                    const copy = [...prev.skills];
                                                    copy[idx] = { ...copy[idx], miscBonus: v };
                                                    return { ...prev, skills: copy };
                                                });
                                            }}
                                            className="w-16 text-center border border-stone-300 rounded p-1 text-sm"
                                            title="Misc bonus"
                                        />
                                    </div>
                                </div>
                            );
                        })}
                    </div>
                )}
            </div>
        </section>
    </>);
}

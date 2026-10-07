import type { CharacterForm } from "./characterTypes";
import { characterCalculations } from "./characterCalculations";
import { abilityAbbrev, isOfficialClass } from "./characterConstants";
import { abilityModUtil, formatModUtil } from "../../Utils/dnd";

export default function CharacterReadSummary({ form }: { form: CharacterForm }) {
    const { prettySkill, formatMod, passivePerception, calcSkillMod, abilities, pb } = characterCalculations(form);
    return (
        <>
            <section className="rounded-2xl border border-stone-200 bg-gradient-to-b from-amber-50 to-stone-50 p-5 shadow-sm">
                {/* Header */}
                <div className="flex flex-col gap-3 md:flex-row md:items-start md:justify-between">
                    <div className="space-y-1">
                        <div className="flex flex-wrap items-baseline gap-x-3 gap-y-1">
                            <h2 className="text-3xl font-extrabold tracking-tight text-stone-900">
                                {form.name}
                            </h2>
                            <span className="inline-flex items-center rounded-full border border-stone-200 bg-white/70 px-3 py-1 text-sm font-semibold text-stone-700">
                                Lv {form.level}
                            </span>
                            <span className="inline-flex items-center rounded-full border border-stone-200 bg-white/70 px-3 py-1 text-sm font-semibold text-stone-700">
                                {form.class}
                                {!isOfficialClass(form.class) && (
                                    <span className="ml-2 rounded-full bg-amber-100 px-2 py-0.5 text-[11px] font-bold text-amber-800 border border-amber-200">
                                        Homebrew
                                    </span>
                                )}
                            </span>
                        </div>

                        <div className="text-sm text-stone-700">
                            <span className="font-semibold">{form.race}</span> •{" "}
                            <span className="text-stone-600">{form.alignment}</span>
                        </div>

                        {(form.background || form.experiencePoints !== undefined) && (
                            <div className="text-xs text-stone-600">
                                <span className="font-semibold">Background:</span>{" "}
                                {form.background || "—"}{" "}
                                <span className="mx-2 text-stone-300">|</span>
                                <span className="font-semibold">XP:</span> {form.experiencePoints ?? 0}
                            </div>
                        )}
                    </div>

                    <div className="flex flex-wrap items-center gap-2">
                        <span className="inline-flex items-center rounded-full border border-stone-200 bg-white/80 px-3 py-1 text-xs font-bold text-stone-700">
                            PB {formatMod(pb)}
                        </span>
                        <span className="inline-flex items-center rounded-full border border-stone-200 bg-white/80 px-3 py-1 text-xs font-bold text-stone-700">
                            Passive Perception {passivePerception(form)}
                        </span>
                        <span className="inline-flex items-center rounded-full border border-stone-200 bg-white/80 px-3 py-1 text-xs font-bold text-stone-700">
                            Inspiration {form.inspiration ? "⭐" : "—"}
                        </span>
                    </div>
                </div>

                <div className="my-5 h-px bg-stone-200" />

                {/* Quick stats */}
                <div className="grid grid-cols-2 gap-3 md:grid-cols-6">
                    <div className="rounded-xl border border-stone-200 bg-white/70 p-3">
                        <div className="text-[11px] font-semibold uppercase tracking-wide text-stone-500">
                            Armor Class
                        </div>
                        <div className="mt-1 text-2xl font-extrabold text-stone-900">
                            {form.armorClass}
                        </div>
                    </div>

                    <div className="rounded-xl border border-stone-200 bg-white/70 p-3">
                        <div className="text-[11px] font-semibold uppercase tracking-wide text-stone-500">
                            Initiative
                        </div>
                        <div className="mt-1 text-2xl font-extrabold text-stone-900">
                            {formatMod(form.initiative)}
                        </div>
                    </div>

                    <div className="rounded-xl border border-stone-200 bg-white/70 p-3">
                        <div className="text-[11px] font-semibold uppercase tracking-wide text-stone-500">
                            Speed
                        </div>
                        <div className="mt-1 text-2xl font-extrabold text-stone-900">{form.speed}</div>
                    </div>

                    <div className="rounded-xl border border-stone-200 bg-white/70 p-3">
                        <div className="text-[11px] font-semibold uppercase tracking-wide text-stone-500">
                            Hit Dice
                        </div>
                        <div className="mt-1 text-lg font-extrabold text-stone-900">
                            {form.hitDice?.remaining ?? 0}/{form.hitDice?.total ?? 0}{" "}
                            <span className="text-sm font-semibold text-stone-600">
                                {form.hitDice?.die ? `(${form.hitDice.die})` : ""}
                            </span>
                        </div>
                    </div>

                    <div className="col-span-2 rounded-xl border border-stone-200 bg-white/70 p-3">
                        <div className="text-[11px] font-semibold uppercase tracking-wide text-stone-500">
                            Hit Points
                        </div>
                        <div className="mt-1 text-2xl font-extrabold text-stone-900">
                            {form.hitPointCurrent}/{form.hitPointMax}
                            {form.hitPointTemporary > 0 && (
                                <span className="ml-2 text-sm font-semibold text-stone-600">
                                    +{form.hitPointTemporary} temp
                                </span>
                            )}
                        </div>
                    </div>
                </div>

                {/* Abilities */}
                <div className="mt-6">
                    <div className="mb-2 flex items-center justify-between">
                        <div className="text-sm font-extrabold tracking-wide text-stone-900">
                            Abilities
                        </div>
                        <div className="text-xs text-stone-600">(Score / Mod)</div>
                    </div>

                    <div className="grid grid-cols-2 gap-2 md:grid-cols-6">
                        {abilities.map((a) => {
                            const mod = abilityModUtil(a.score);
                            return (
                                <div
                                    key={a.key}
                                    className="rounded-xl border border-stone-200 bg-white/70 p-3 text-center"
                                >
                                    <div className="text-[11px] font-bold uppercase tracking-wide text-stone-500">
                                        {a.key}
                                    </div>
                                    <div className="mt-1 text-xl font-extrabold text-stone-900">
                                        {a.score}
                                    </div>
                                    <div className="text-sm font-semibold text-stone-700">
                                        {formatModUtil(mod)}
                                    </div>
                                </div>
                            );
                        })}
                    </div>
                </div>

                {/* Skills + Attacks */}
                <div className="mt-6 grid grid-cols-1 gap-4 md:grid-cols-2">
                    {/* Skills */}
                    <div className="rounded-2xl border border-stone-200 bg-white/70 shadow-sm overflow-hidden">
                        <div className="flex items-center justify-between border-b border-stone-200 px-4 py-3">
                            <div className="text-sm font-extrabold text-stone-900">Skills</div>
                            <div className="text-xs text-stone-600">PB {formatMod(pb)}</div>
                        </div>

                        {form.skills?.length ? (
                            <ul className="divide-y divide-stone-200">
                                {form.skills.map((s, idx) => {
                                    const bonus = calcSkillMod(s);

                                    return (
                                        <li
                                            key={`${s.skill}-${idx}`}
                                            className="flex items-center justify-between px-4 py-2"
                                        >
                                            <div className="min-w-0">
                                                <div className="truncate text-sm font-semibold text-stone-900">
                                                    {prettySkill(String(s.skill))}
                                                    <span className="ml-2 text-xs font-semibold text-stone-500">
                                                        ({abilityAbbrev(s.ability)})
                                                    </span>
                                                </div>

                                                <div className="mt-1 flex flex-wrap gap-2">
                                                    {s.isProficient && (
                                                        <span className="rounded-full bg-emerald-100 px-2 py-0.5 text-[11px] font-bold text-emerald-800">
                                                            PROF
                                                        </span>
                                                    )}
                                                    {s.isExpertise && (
                                                        <span className="rounded-full bg-indigo-100 px-2 py-0.5 text-[11px] font-bold text-indigo-800">
                                                            EXP
                                                        </span>
                                                    )}
                                                    {(s.miscBonus ?? 0) !== 0 && (
                                                        <span className="rounded-full bg-amber-100 px-2 py-0.5 text-[11px] font-bold text-amber-800">
                                                            {s.miscBonus! > 0 ? `+${s.miscBonus}` : s.miscBonus}
                                                        </span>
                                                    )}
                                                </div>
                                            </div>

                                            <div className="ml-4 shrink-0 rounded-lg border border-stone-200 bg-white px-3 py-1 text-sm font-extrabold text-stone-900">
                                                {formatMod(bonus)}
                                            </div>
                                        </li>
                                    );
                                })}
                            </ul>
                        ) : (
                            <div className="px-4 py-4 text-sm text-stone-600">No skills yet.</div>
                        )}
                    </div>

                    {/* Attacks */}
                    <div className="rounded-2xl border border-stone-200 bg-white/70 shadow-sm overflow-hidden">
                        <div className="flex items-center justify-between border-b border-stone-200 px-4 py-3">
                            <div className="text-sm font-extrabold text-stone-900">Attacks</div>
                            <div className="text-xs text-stone-600">Name / Bonus / Damage</div>
                        </div>

                        {form.attacks?.length ? (
                            <ul className="divide-y divide-stone-200">
                                {form.attacks.map((a, idx) => (
                                    <li key={a.clientId ?? `${a.name}-${idx}`} className="px-4 py-3">
                                        <div className="flex items-center justify-between gap-3">
                                            <div className="min-w-0">
                                                <div className="truncate text-sm font-semibold text-stone-900">
                                                    {a.name || "—"}
                                                </div>
                                                <div className="mt-0.5 text-xs text-stone-600 truncate">
                                                    {a.damage || "No damage text"}
                                                </div>
                                            </div>

                                            <div className="shrink-0 rounded-lg border border-stone-200 bg-white px-3 py-1 text-sm font-extrabold text-stone-900">
                                                {formatMod(a.attackBonus)}
                                            </div>
                                        </div>
                                    </li>
                                ))}
                            </ul>
                        ) : (
                            <div className="px-4 py-4 text-sm text-stone-600">No attacks yet.</div>
                        )}
                    </div>
                </div>
            </section>
        </>
    );
}

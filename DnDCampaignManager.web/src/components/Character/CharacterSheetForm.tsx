import FormCard from "../UI/FormCard";
import Button from "../UI/Button";
import ErrorPanel from "../UI/ErrorPanel";

type SkillType =
    | "Acrobatics"
    | "AnimalHandling"
    | "Arcana"
    | "Athletics"
    | "Deception"
    | "History"
    | "Insight"
    | "Intimidation"
    | "Investigation"
    | "Medicine"
    | "Nature"
    | "Perception"
    | "Performance"
    | "Persuasion"
    | "Religion"
    | "SleightOfHand"
    | "Stealth"
    | "Survival";

type AbilityType =
    | "Strength"
    | "Dexterity"
    | "Constitution"
    | "Intelligence"
    | "Wisdom"
    | "Charisma";

type AbilityKeyLower =
    | "strength"
    | "dexterity"
    | "constitution"
    | "intelligence"
    | "wisdom"
    | "charisma";

export type CharacterSkill = {
    skill: SkillType;
    ability: AbilityType;
    isProficient: boolean;
    isExpertise: boolean;
    miscBonus: number;
};

export type SavingThrow = {
    isProficient: boolean;
    miscBonus: number;
};

type HitDice = {
    die: string;          // e.g. "d8"
    total: number;        // max hit dice (usually = level)
    remaining: number;    // spent during short rests
};

type Attack = {
    id?: number | null; 
    clientId: string;
    name: string;         
    attackBonus: number;  
    damage: string;       
};

export type CharacterForm = {
    // Identity
    name: string;
    class: string;
    race: string;
    level: number;
    background: string;
    alignment: string;
    experiencePoints: number;

    // Combat
    armorClass: number;
    initiative: number;
    speed: number;

    // HP
    hitPointMax: number;
    hitPointCurrent: number;
    hitPointTemporary: number;

    // Abilities
    strength: number;
    dexterity: number;
    constitution: number;
    intelligence: number;
    wisdom: number;
    charisma: number;

    inspiration: boolean;

    // Saving throws
    savingThrows: {
        strength: SavingThrow;
        dexterity: SavingThrow;
        constitution: SavingThrow;
        intelligence: SavingThrow;
        wisdom: SavingThrow;
        charisma: SavingThrow;
    };

    // Death saves
    deathSaves: {
        successes: number; 
        failures: number;  
    };

    hitDice: HitDice;
    attacks: Attack[];
    skills: CharacterSkill[];
};

type Props = {
    title: string;
    subtitle?: string;
    topRight?: React.ReactNode;

    form: CharacterForm;
    setForm: React.Dispatch<React.SetStateAction<CharacterForm>>;

    onCancel: () => void;
    onSubmit: (e: React.FormEvent) => void;

    submitLabel: string;
    submitting?: boolean;
    error?: string | null;
};

export default function CharacterSheetForm({
    title,
    subtitle,
    topRight,
    form,
    setForm,
    onCancel,
    onSubmit,
    submitLabel,
    submitting,
    error
}: Props) {
    const handleChange = (
        e: React.ChangeEvent<HTMLInputElement | HTMLTextAreaElement>
    ) => {
        const { name, value } = e.target;
        const isNumber =
            e.target instanceof HTMLInputElement && e.target.type === "number";

        setForm((prev) => ({
            ...prev,
            [name]: isNumber ? Number(value) : value,
        }));
    };

    const abilityAbbrev = (a: AbilityType) =>
    ({
        Strength: "STR",
        Dexterity: "DEX",
        Constitution: "CON",
        Intelligence: "INT",
        Wisdom: "WIS",
        Charisma: "CHA",
    }[a]);

    const prettySkill = (s: string) => s.replace(/([A-Z])/g, " $1").trim();

    const proficiencyFromLevel = (level: number) =>
        2 + Math.floor((Math.max(1, level) - 1) / 4);

    const abilityMod = (score: number) => Math.floor((score - 10) / 2);

    const abilityScoreFor = (ability: AbilityType) => {
        switch (ability) {
            case "Strength": return form.strength;
            case "Dexterity": return form.dexterity;
            case "Constitution": return form.constitution;
            case "Intelligence": return form.intelligence;
            case "Wisdom": return form.wisdom;
            case "Charisma": return form.charisma;
        }
    };

    const savingThrowTotal = (key: keyof CharacterForm["savingThrows"], f: CharacterForm) => {
        const pb = proficiencyFromLevel(f.level);
        const mod = abilityMod(abilityScoreForKey(key));
        const st = f.savingThrows?.[key] ?? { isProficient: false, miscBonus: 0 };
        return mod + (st.isProficient ? pb : 0) + (st.miscBonus ?? 0);
    };

    const passivePerception = (form: any) => {
        const pb = proficiencyFromLevel(form.level);
        const wisMod = abilityMod(form.wisdom);
        const perception = form.skills?.find((s: any) => String(s.skill) === "Perception");
        const prof = perception?.isProficient ? pb : 0;
        const exp = perception?.isExpertise ? pb : 0;
        const misc = perception?.miscBonus ?? 0;
        return 10 + wisMod + prof + exp + misc;
    };

    const formatMod = (n: number) => (n >= 0 ? `+${n}` : `${n}`);

    const calcSkillMod = (sk: CharacterSkill) => {
        const pb = proficiencyFromLevel(form.level);
        const mod = abilityMod(abilityScoreFor(sk.ability));
        const prof = sk.isProficient ? pb : 0;
        const exp = sk.isExpertise ? pb : 0;
        const misc = sk.miscBonus ?? 0;
        return mod + prof + exp + misc;
    };


    const abilityScoreForKey = (key: AbilityKeyLower) => form[key];

    return (
        <FormCard title={title} subtitle={subtitle} topRight={topRight}>
            {error && <ErrorPanel message={error} />}

            <form onSubmit={onSubmit} className="space-y-6">
                {/* Identity */}
                <section className="bg-stone-50 border border-stone-300 rounded-lg p-4">
                    <h2 className="text-lg font-semibold text-stone-700 mb-4">Identity</h2>

                    <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
                        <div className="md:col-span-2">
                            <label className="block text-sm font-semibold text-stone-700">
                                Character Name
                            </label>
                            <input
                                name="name"
                                value={form.name}
                                onChange={handleChange}
                                className="w-full border border-stone-400 rounded-md p-2 bg-white"
                                required
                            />
                        </div>

                        <div>
                            <label className="block text-sm font-semibold text-stone-700">
                                Level
                            </label>
                            <input
                                type="number"
                                name="level"
                                min={1}
                                max={20}
                                value={form.level}
                                onChange={handleChange}
                                className="w-full border border-stone-400 rounded-md p-2 bg-white"
                            />
                        </div>

                        <div>
                            <label className="block text-sm font-semibold text-stone-700">
                                Class
                            </label>
                            <input
                                name="class"
                                value={form.class}
                                onChange={handleChange}
                                className="w-full border border-stone-400 rounded-md p-2 bg-white"
                                required
                            />
                        </div>

                        <div>
                            <label className="block text-sm font-semibold text-stone-700">
                                Race
                            </label>
                            <input
                                name="race"
                                value={form.race}
                                onChange={handleChange}
                                className="w-full border border-stone-400 rounded-md p-2 bg-white"
                                required
                            />
                        </div>

                        <div>
                            <label className="block text-sm font-semibold text-stone-700">
                                Background
                            </label>
                            <input
                                name="background"
                                value={form.background}
                                onChange={handleChange}
                                className="w-full border border-stone-400 rounded-md p-2 bg-white"
                            />
                        </div>

                        <div>
                            <label className="block text-sm font-semibold text-stone-700">
                                Alignment
                            </label>
                            <input
                                name="alignment"
                                value={form.alignment}
                                onChange={handleChange}
                                className="w-full border border-stone-400 rounded-md p-2 bg-white"
                            />
                        </div>

                        <div>
                            <label className="block text-sm font-semibold text-stone-700">
                                XP
                            </label>
                            <input
                                type="number"
                                name="experiencePoints"
                                min={0}
                                value={form.experiencePoints}
                                onChange={handleChange}
                                className="w-full border border-stone-400 rounded-md p-2 bg-white"
                            />
                        </div>
                    </div>
                </section>

                {/* Abilities + Skills */}
                <section className="grid grid-cols-1 md:grid-cols-3 gap-6">
                    <div className="bg-stone-50 border border-stone-300 rounded-lg p-4">
                        <h2 className="text-lg font-semibold text-stone-700 mb-4">
                            Ability Scores
                        </h2>

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
                                    min={1}
                                    max={30}
                                    value={form[key]}
                                    onChange={handleChange}
                                    className="w-24 text-center border border-stone-400 rounded-md p-1 bg-white"
                                />
                            </div>
                        ))}
                    </div>

                    <div className="bg-stone-50 border border-stone-300 rounded-lg p-4 md:col-span-2">
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

                {/* Saving throws */}
                <section className="bg-stone-50 border border-stone-300 rounded-lg p-4">
                    <h2 className="text-lg font-semibold text-stone-700 mb-4">Saving Throws</h2>

                    <div className="space-y-2">
                        {(
                            [
                                ["strength", "STR"],
                                ["dexterity", "DEX"],
                                ["constitution", "CON"],
                                ["intelligence", "INT"],
                                ["wisdom", "WIS"],
                                ["charisma", "CHA"],
                            ] as const
                        ).map(([key, label]) => {
                            const total = savingThrowTotal(key, form);
                            const st = form.savingThrows?.[key] ?? { isProficient: false, miscBonus: 0 };

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
                                        {/* computed value */}
                                        <div className="w-10 text-center font-bold text-stone-800">
                                            {formatMod(total)}
                                        </div>

                                        {/* proficient */}
                                        <label className="text-xs text-stone-600 flex items-center gap-1">
                                            Prof
                                            <input
                                                type="checkbox"
                                                checked={!!st.isProficient}
                                                onChange={e =>
                                                    setForm(prev => ({
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

                                        {/* misc */}
                                        <input
                                            type="number"
                                            value={st.miscBonus ?? 0}
                                            onChange={e =>
                                                setForm(prev => ({
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
                    </div>
                </section>

                {/* Combat + HP */}
                <section className="bg-stone-50 border border-stone-300 rounded-lg p-4">
                    <h2 className="text-lg font-semibold text-stone-700 mb-4">Combat</h2>

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

                {/* Hit Dice */}
                <section className="bg-stone-50 border border-stone-300 rounded-lg p-4">
                    <h2 className="text-lg font-semibold text-stone-700 mb-4">Hit Dice</h2>

                    <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
                        <div>
                            <label className="block text-sm font-semibold text-stone-700">Die</label>
                            <input
                                name="hitDice.die"
                                value={form.hitDice.die}
                                onChange={e =>
                                    setForm(prev => ({
                                        ...prev,
                                        hitDice: { ...prev.hitDice, die: e.target.value }
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
                                value={form.hitDice.total}
                                onChange={e => {
                                    const v = Number(e.target.value);
                                    setForm(prev => ({
                                        ...prev,
                                        hitDice: {
                                            ...prev.hitDice,
                                            total: v,
                                            remaining: Math.min(prev.hitDice.remaining, v),
                                        }
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
                                onChange={e => {
                                    const v = Number(e.target.value);
                                    setForm(prev => ({
                                        ...prev,
                                        hitDice: {
                                            ...prev.hitDice,
                                            remaining: Math.max(0, Math.min(v, prev.hitDice.total))
                                        }
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

                {/* Death Saves */}
                <section className="bg-stone-50 border border-stone-300 rounded-lg p-4">
                    <h2 className="text-lg font-semibold text-stone-700 mb-4">Death Saves</h2>

                    <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                        {/* Successes */}
                        <div className="border border-stone-200 rounded-md bg-white px-3 py-2">
                            <div className="font-semibold text-stone-800 mb-2">Successes</div>
                            <div className="flex gap-2">
                                {[0, 1, 2].map(i => (
                                    <input
                                        key={i}
                                        type="checkbox"
                                        checked={(form.deathSaves?.successes ?? 0) > i}
                                        onChange={() => {
                                            const current = form.deathSaves?.successes ?? 0;
                                            const next = current > i ? i : i + 1;
                                            setForm(prev => ({
                                                ...prev,
                                                deathSaves: { ...prev.deathSaves, successes: next },
                                            }));
                                        }}
                                        className="h-5 w-5"
                                    />
                                ))}
                            </div>
                        </div>

                        {/* Failures */}
                        <div className="border border-stone-200 rounded-md bg-white px-3 py-2">
                            <div className="font-semibold text-stone-800 mb-2">Failures</div>
                            <div className="flex gap-2">
                                {[0, 1, 2].map(i => (
                                    <input
                                        key={i}
                                        type="checkbox"
                                        checked={(form.deathSaves?.failures ?? 0) > i}
                                        onChange={() => {
                                            const current = form.deathSaves?.failures ?? 0;
                                            const next = current > i ? i : i + 1;
                                            setForm(prev => ({
                                                ...prev,
                                                deathSaves: { ...prev.deathSaves, failures: next },
                                            }));
                                        }}
                                        className="h-5 w-5"
                                    />
                                ))}
                            </div>
                        </div>
                    </div>
                </section>

                {/* Misc */}
                <section className="bg-stone-50 border border-stone-300 rounded-lg p-4">
                    <h2 className="text-lg font-semibold text-stone-700 mb-4">Core</h2>

                    <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
                        {/* Inspiration */}
                        <div className="flex items-center justify-between border border-stone-200 rounded-md bg-white px-3 py-2">
                            <div>
                                <div className="font-semibold text-stone-800">Inspiration</div>
                                <div className="text-xs text-stone-500">Toggle when earned</div>
                            </div>
                            <input
                                type="checkbox"
                                checked={!!form.inspiration}
                                onChange={e =>
                                    setForm(prev => ({ ...prev, inspiration: e.target.checked }))
                                }
                                className="h-5 w-5"
                            />
                        </div>

                        {/* Proficiency Bonus */}
                        <div className="flex items-center justify-between border border-stone-200 rounded-md bg-white px-3 py-2">
                            <div>
                                <div className="font-semibold text-stone-800">Proficiency Bonus</div>
                                <div className="text-xs text-stone-500">From level</div>
                            </div>
                            <div className="font-bold text-stone-800">
                                {formatMod(proficiencyFromLevel(form.level))}
                            </div>
                        </div>

                        {/* Passive Perception */}
                        <div className="flex items-center justify-between border border-stone-200 rounded-md bg-white px-3 py-2">
                            <div>
                                <div className="font-semibold text-stone-800">Passive Perception</div>
                                <div className="text-xs text-stone-500">10 + Perception mod</div>
                            </div>
                            <div className="font-bold text-stone-800">
                                {passivePerception(form)}
                            </div>
                        </div>
                    </div>
                </section>

                {/* Attacks & Spellcasting */}
                <section className="bg-stone-50 border border-stone-300 rounded-lg p-4">
                    <div className="flex items-center justify-between mb-4">
                        <h2 className="text-lg font-semibold text-stone-700">Attacks & Spellcasting</h2>

                        <button
                            type="button"
                            onClick={() =>
                                setForm(prev => ({
                                    ...prev,
                                    attacks: [
                                        ...(prev.attacks ?? []),
                                        {
                                            id: null, // 👈 new items have no server id yet
                                            clientId: crypto.randomUUID(), // 👈 UI-only unique key
                                            name: "",
                                            attackBonus: 0,
                                            damage: ""
                                        }
                                    ]
                                }))
                            }
                            className="px-3 py-1.5 rounded-md bg-stone-900 text-amber-50 text-sm font-semibold hover:bg-stone-800"
                        >
                            + Add
                        </button>
                    </div>

                    {(form.attacks?.length ?? 0) === 0 ? (
                        <p className="text-stone-600 text-sm">No attacks yet.</p>
                    ) : (
                        <div className="space-y-3">
                            {form.attacks!.map((a, idx) => (
                                <div
                                    key={a.clientId} // 👈 stable unique key
                                    className="grid grid-cols-1 md:grid-cols-12 gap-3 border border-stone-200 rounded-md bg-white p-3"
                                >
                                    <div className="md:col-span-5">
                                        <label className="block text-xs font-semibold text-stone-600">Name</label>
                                        <input
                                            value={a.name}
                                            onChange={e => {
                                                const v = e.target.value;
                                                setForm(prev => {
                                                    const copy = [...(prev.attacks ?? [])];
                                                    copy[idx] = { ...copy[idx], name: v };
                                                    return { ...prev, attacks: copy };
                                                });
                                            }}
                                            className="w-full border border-stone-300 rounded-md p-2"
                                            placeholder="Longsword"
                                        />
                                    </div>

                                    <div className="md:col-span-3">
                                        <label className="block text-xs font-semibold text-stone-600">Atk Bonus</label>
                                        <input
                                            type="number"
                                            value={a.attackBonus}
                                            onChange={e => {
                                                const v = Number(e.target.value);
                                                setForm(prev => {
                                                    const copy = [...(prev.attacks ?? [])];
                                                    copy[idx] = { ...copy[idx], attackBonus: v };
                                                    return { ...prev, attacks: copy };
                                                });
                                            }}
                                            className="w-full border border-stone-300 rounded-md p-2"
                                        />
                                    </div>

                                    <div className="md:col-span-4">
                                        <label className="block text-xs font-semibold text-stone-600">Damage/Type</label>
                                        <input
                                            value={a.damage}
                                            onChange={e => {
                                                const v = e.target.value;
                                                setForm(prev => {
                                                    const copy = [...(prev.attacks ?? [])];
                                                    copy[idx] = { ...copy[idx], damage: v };
                                                    return { ...prev, attacks: copy };
                                                });
                                            }}
                                            className="w-full border border-stone-300 rounded-md p-2"
                                            placeholder="1d8+3 slashing"
                                        />
                                    </div>

                                    <div className="md:col-span-12 flex justify-end">
                                        <button
                                            type="button"
                                            onClick={() => {
                                                if (confirm("Remove this attack?")) {
                                                    setForm(prev => ({
                                                        ...prev,
                                                        attacks: (prev.attacks ?? []).filter(x => x.clientId !== a.clientId)
                                                    }));
                                                }
                                            }}
                                            className="px-3 py-1.5 rounded-md border border-stone-300 bg-stone-100 hover:bg-stone-200 text-stone-700 text-sm"
                                        >
                                            Remove
                                        </button>
                                    </div>
                                </div>
                            ))}
                        </div>
                    )}

                    <p className="mt-2 text-xs text-stone-600">
                        Use this for weapon attacks or spell attacks (e.g., Fire Bolt +5, 1d10 fire).
                    </p>
                </section>


                {/* Actions */}
                <div className="flex justify-end gap-3">
                    <Button type="button" variant="subtle" onClick={onCancel} disabled={submitting}>
                        Cancel
                    </Button>

                    <Button type="submit" variant="primary" disabled={submitting}>
                        {submitting ? "Saving..." : submitLabel}
                    </Button>
                </div>
            </form>
        </FormCard>
    );
}

import FormCard from "../UI/FormCard";
import Button from "../UI/Button";
import ErrorPanel from "../UI/ErrorPanel";
import { abilityModUtil, formatModUtil } from "../../Utils/dnd";
import React, { useEffect, useMemo, useState } from "react";
import {
    addCharacterClass,
    getCharacterClass,
    addCharacterRace,
    getCharacterRaces,
    getCharacterBackgrounds,
    addCharacterBackground
} from "../../api/campaignApi";

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

export type CharacterClassOption = {
    id: number;
    name: string;
    isCustom: boolean;
};

type HitDice = {
    die: string; 
    total: number; 
    remaining: number; 
};

type Attack = {
    id?: number | null;
    clientId: string;
    name: string;
    attackBonus: number;
    damage: string;
};

export type CharacterForm = {
    campaignId: number;
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

    hitDice: HitDice;
    attacks: Attack[];
    skills: CharacterSkill[];
};

type CharacterRaceOptionDto = {
    id: number;
    name: string;
    isCustom: boolean
};

type CharacterBackgroundOptionDto ={
        id: number;
        name: string;
        isCustom: boolean
};

type SavingThrowKey = typeof SAVING_THROW_ROWS[number]["key"];

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

const SAVING_THROW_ROWS = [
    { key: "strength", label: "STR" },
    { key: "dexterity", label: "DEX" },
    { key: "constitution", label: "CON" },
    { key: "intelligence", label: "INT" },
    { key: "wisdom", label: "WIS" },
    { key: "charisma", label: "CHA" },
] as const;

const CLASS_OPTIONS = [
    "Barbarian",
    "Bard",
    "Cleric",
    "Druid",
    "Fighter",
    "Monk",
    "Paladin",
    "Ranger",
    "Rogue",
    "Sorcerer",
    "Warlock",
    "Wizard",
    "Artificer",
] as const;

const ALIGNMENT_OPTIONS = [
    "Lawful Good",
    "Neutral Good",
    "Chaotic Good",
    "Lawful Neutral",
    "Neutral",
    "Chaotic Neutral",
    "Lawful Evil",
    "Neutral Evil",
    "Chaotic Evil",
] as const;

const RACE_OPTIONS = [
    "Dragonborn",
    "Dwarf",
    "Elf",
    "Gnome",
    "Half-Elf",
    "Half-Orc",
    "Halfling",
    "Human",
    "Tiefling",
] as const;

const BACKGROUND_OPTIONS = [
    "Acolyte",
    "Charlatan",
    "Criminal",
    "Entertainer",
    "Folk Hero",
    "Guild Artisan",
    "Hermit",
    "Noble",
    "Outlander",
    "Sage",
    "Sailor",
    "Soldier",
    "Urchin"
] as const;

const isOfficialClass = (name: string) =>
    CLASS_OPTIONS.some((x) => x.toLowerCase() === name.toLowerCase());

const isOfficialRace = (name: string) =>
    RACE_OPTIONS.some(r => r.toLowerCase() === name.toLowerCase());

const isOfficialBackground = (name: string) =>
    BACKGROUND_OPTIONS.some(r => r.toLowerCase() === name.toLowerCase())

const abilityAbbrev = (a: AbilityType) =>
({
    Strength: "STR",
    Dexterity: "DEX",
    Constitution: "CON",
    Intelligence: "INT",
    Wisdom: "WIS",
    Charisma: "CHA",
}[a]);

export default function CharacterSheetForm({
    title,
    subtitle,
    form,
    setForm,
    onCancel,
    onSubmit,
    submitLabel,
    submitting,
    error,
}: Props) {
    // --- Shared styling helpers (keeps things consistent) ---
    const sectionClass =
        "bg-stone-50 border border-stone-300 rounded-xl p-4";
    const sectionTitleClass =
        "text-lg font-semibold text-stone-700 mb-4";

    const handleChange = (
        e: React.ChangeEvent<HTMLInputElement | HTMLTextAreaElement | HTMLSelectElement>
    ) => {
        const { name, value } = e.target;
        const isNumber =
            e.target instanceof HTMLInputElement && e.target.type === "number";

        setForm((prev) => ({
            ...prev,
            [name]: isNumber ? Number(value) : value,
        }));
    };

    const prettySkill = (s: string) => s.replace(/([A-Z])/g, " $1").trim();

    const proficiencyFromLevel = (level: number) =>
        2 + Math.floor((Math.max(1, level) - 1) / 4);

    const abilityMod = (score: number) => abilityModUtil(score);
    const formatMod = (n: number) => formatModUtil(n);

    const abilityScoreFor = (ability: AbilityType) => {
        switch (ability) {
            case "Strength":
                return form.strength;
            case "Dexterity":
                return form.dexterity;
            case "Constitution":
                return form.constitution;
            case "Intelligence":
                return form.intelligence;
            case "Wisdom":
                return form.wisdom;
            case "Charisma":
                return form.charisma;
        }
    };

    const savingThrowTotal = (key: SavingThrowKey, f: CharacterForm) => {
        const pb = proficiencyFromLevel(f.level);
        const mod = abilityMod(f[key]); // key is one of the ability keys
        const st = f.savingThrows[key] ?? { isProficient: false, miscBonus: 0 };
        return mod + (st.isProficient ? pb : 0) + (st.miscBonus ?? 0);
    }; 

    const passivePerception = (f: CharacterForm) => {
        const pb = proficiencyFromLevel(f.level);
        const wisMod = abilityMod(f.wisdom);
        const perception = f.skills?.find((s) => String(s.skill) === "Perception");
        const prof = perception?.isProficient ? pb : 0;
        const exp = perception?.isExpertise ? pb : 0;
        const misc = perception?.miscBonus ?? 0;
        return 10 + wisMod + prof + exp + misc;
    };

    const calcSkillMod = (sk: CharacterSkill) => {
        const pb = proficiencyFromLevel(form.level);
        const mod = abilityMod(abilityScoreFor(sk.ability));
        const prof = sk.isProficient ? pb : 0;
        const exp = sk.isExpertise ? pb : 0;
        const misc = sk.miscBonus ?? 0;
        return mod + prof + exp + misc;
    };

    const [isReadView, setIsReadView] = useState(false);

    const abilities = [
        { key: "Strength", score: form.strength },
        { key: "Dexterity", score: form.dexterity },
        { key: "Constitution", score: form.constitution },
        { key: "Intelligence", score: form.intelligence },
        { key: "Wisdom", score: form.wisdom },
        { key: "Charisma", score: form.charisma },
    ];

    const pb = proficiencyFromLevel(form.level);

    // Class options + homebrew handling
    const [classOptions, setClassOptions] = useState<string[]>([]);
    const [classLoading, setClassLoading] = useState(false);
    const [classError, setClassError] = useState<string | null>(null);
    const [addingClass, setAddingClass] = useState(false);
    const [newClassName, setNewClassName] = useState("");

    // Race options + homebrew handling
    const [raceOptions, setRaceOptions] = useState<CharacterRaceOptionDto[]>([]);
    const [raceLoading, setRaceLoading] = useState(false);
    const [raceError, setRaceError] = useState<string | null>(null);
    const [addingRace, setAddingRace] = useState(false);
    const [newRaceName, setNewRaceName] = useState("");

    // Background options + homebrew handling
    const [backgroundOptions, setBackgroundOptions] = useState<CharacterBackgroundOptionDto[]>([]);
    const [backgroundLoading, setBackgroundLoading] = useState(false);
    const [backgroundError, setBackgroundError] = useState<string | null>(null);
    const [addingBackground, setAddingBackground] = useState(false);
    const [newBackgroundName, setNewBackgroundName] = useState("");

    async function loadClassOptions(campaignId: number) {
        setClassLoading(true);
        setClassError(null);
        try {
            const res = await getCharacterClass(campaignId);
            const custom = res.data.map((x: any) => x.name as string);

            const merged = [...CLASS_OPTIONS, ...custom]
                .filter(Boolean)
                .reduce<string[]>((acc, name) => {
                    const exists = acc.some(x => x.toLowerCase() === name.toLowerCase());
                    if (!exists) acc.push(name);
                    return acc;
                }, []);

            setClassOptions(merged);
        } catch {
            setClassError("Could not load classes for this campaign.");
        } finally {
            setClassLoading(false);
        }
    }

    async function loadRaceOptions(campaignId: number) {
        setRaceLoading(true);
        setRaceError(null);
        try {
            const res = await getCharacterRaces(campaignId);
            setRaceOptions(res.data);
        } catch {
            setRaceError("Could not load races for this campaign.");
        } finally {
            setRaceLoading(false);
        }
    }

    async function loadBackgroundOptions(campaignId: number) {
        setBackgroundLoading(true);
        setBackgroundError(null);
        try {
            const res = await getCharacterBackgrounds(campaignId);
            setBackgroundOptions(res.data);
        } catch {
            setBackgroundError("Could not load background for this campaign.");
        } finally {
            setBackgroundLoading(false);
        }
    }

    // Handle adding homebrew class
    async function handleAddClass() {
        const name = newClassName.trim();
        if (!name || !form.campaignId) return;

        try {
            setClassError(null);

            const res = await addCharacterClass(form.campaignId, { name });
            const addedName = res.data.name as string;

            setClassOptions(prev => {
                if (prev.some(x => x.toLowerCase() === addedName.toLowerCase())) return prev;
                return [...prev, addedName].sort((a, b) => a.localeCompare(b));
            });

            setForm(prev => ({ ...prev, class: addedName }));
            setAddingClass(false);
            setNewClassName("");
        } catch (err: any) {
            const msg = err?.response?.data;
            setClassError(typeof msg === "string" ? msg : "Could not add class.");
        }
    }

    // Handle adding homebrew race
    async function handleAddRace() {
        const name = newRaceName.trim();
        if (!name || !form.campaignId) return;

        try {
            setRaceError(null);
            const res = await addCharacterRace(form.campaignId, { name });
            const added = res.data as CharacterRaceOptionDto;

            setRaceOptions(prev => {
                if (prev.some(x => x.name.toLowerCase() === added.name.toLowerCase())) return prev;
                return [...prev, added];
            });

            setForm(prev => ({ ...prev, race: added.name }));
            setAddingRace(false);
            setNewRaceName("");
        } catch (err: any) {
            const msg = err?.response?.data;
            setRaceError(typeof msg === "string" ? msg : "Could not add race.");
        }
    }

    // Handle adding homebrew background
    async function handleAddBackground() {
        const name = newBackgroundName.trim();
        if (!name || !form.campaignId) return;

        try {
            setBackgroundError(null);

            const res = await addCharacterBackground(form.campaignId, { name });
            const added = res.data as CharacterBackgroundOptionDto;

            setBackgroundOptions(prev => {
                if (prev.some(x => x.name.toLowerCase() === added.name.toLowerCase())) return prev;
                return [...prev, added];
            });

            setForm(prev => ({ ...prev, background: added.name }));
            setAddingBackground(false);
            setNewBackgroundName("");
        } catch (err: any) {
            const msg = err?.response?.data;
            setBackgroundError(typeof msg === "string" ? msg : "Could not add background.");
        }
    }

    const classNames = useMemo(
        () => [...new Set(classOptions)].sort((a, b) => a.localeCompare(b)),
        [classOptions]
    );

    const raceNames = useMemo(() => {
        const names = raceOptions.map(x => x.name).filter(Boolean);
        return Array.from(new Set(names.map(n => n.trim())))
            .sort((a, b) => a.localeCompare(b));
    }, [raceOptions]);

    const backgroundNames = useMemo(() => {
        const names = backgroundOptions.map(x => x.name).filter(Boolean);
        return Array.from(new Set(names.map(n => n.trim())))
            .sort((a, b) => a.localeCompare(b));
    }, [backgroundOptions]);

    useEffect(() => {
        if (!form.campaignId) return;
        loadClassOptions(form.campaignId);
        loadRaceOptions(form.campaignId);
        loadBackgroundOptions(form.campaignId);
    }, [form.campaignId]);

    // -------------------- EDIT VIEW --------------------
    const EditView = (
        <FormCard
            title={title}
            subtitle={subtitle}
            topRight={
                <Button type="button" variant="ghost" onClick={() => setIsReadView(true)}>
                    Read View
                </Button>
            }
        >
            {error && <ErrorPanel message={error} />}

            <form onSubmit={onSubmit} className="space-y-6">
                {/* Identity */}
                <section className={sectionClass}>
                    <h2 className={sectionTitleClass}>Identity</h2>

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

                        {/* Class + Homebrew */}
                        <div>
                            <div className="flex items-center justify-between">
                                <label className="block text-sm font-semibold text-stone-700">
                                    Class
                                </label>

                                <button
                                    type="button"
                                    className="text-xs font-semibold text-emerald-800 hover:underline"
                                    onClick={() => setAddingClass((v) => !v)}
                                >
                                    {addingClass ? "Close" : "Add homebrew"}
                                </button>
                            </div>

                            <select
                                name="class"
                                value={form.class}
                                onChange={handleChange}
                                className="w-full border border-stone-400 rounded-md p-2 bg-white"
                                required
                                disabled={classLoading}
                            >
                                <option value="" disabled>
                                    {classLoading ? "Summoning classes..." : "Choose a class"}
                                </option>

                                <optgroup label="Official">
                                    {classNames
                                        .filter((c) => isOfficialClass(c))
                                        .map((cls) => (
                                            <option key={cls} value={cls}>
                                                {cls}
                                            </option>
                                        ))}
                                </optgroup>

                                <optgroup label="Homebrew">
                                    {classNames
                                        .filter((c) => !isOfficialClass(c))
                                        .map((cls) => (
                                            <option key={cls} value={cls}>
                                                {cls}
                                            </option>
                                        ))}
                                </optgroup>
                            </select>

                            {classError && <p className="mt-1 text-xs text-red-700">{classError}</p>}

                            {addingClass && (
                                <div className="mt-2 rounded-lg border border-stone-300 bg-amber-50 p-3">
                                    <p className="text-xs text-stone-600 mb-2">
                                        Add a homebrew class to this campaign’s roster.
                                    </p>

                                    <div className="flex gap-2">
                                        <input
                                            value={newClassName}
                                            onChange={(e) => setNewClassName(e.target.value)}
                                            placeholder="e.g. Blood Hunter"
                                            className="flex-1 border border-stone-400 rounded-md p-2 bg-white"
                                        />
                                        <button
                                            type="button"
                                            onClick={handleAddClass}
                                            className="px-3 py-2 text-sm font-semibold rounded-lg bg-emerald-700 text-white hover:bg-emerald-600"
                                        >
                                            Inscribe
                                        </button>
                                    </div>
                                </div>
                            )}
                        </div>

                        {/* Race + Homebrew */}
                        <div>
                            <div className="flex items-center justify-between">
                                <label className="block text-sm font-semibold text-stone-700">
                                    Race
                                </label>

                                <button
                                    type="button"
                                    className="text-xs font-semibold text-emerald-800 hover:underline"
                                    onClick={() => setAddingRace(v => !v)}
                                >
                                    {addingRace ? "Close" : "Add homebrew"}
                                </button>
                            </div>

                            <select
                                name="race"
                                value={form.race}
                                onChange={handleChange}
                                className="w-full border border-stone-400 rounded-md p-2 bg-white"
                                required
                                disabled={raceLoading}
                            >
                                <option value="" disabled>
                                    {raceLoading ? "Summoning races..." : "Choose a race"}
                                </option>

                                <optgroup label="Official">
                                    {raceNames.filter(isOfficialRace).map(r => (
                                        <option key={r} value={r}>{r}</option>
                                    ))}
                                </optgroup>

                                <optgroup label="Homebrew">
                                    {raceNames.filter(r => !isOfficialRace(r)).map(r => (
                                        <option key={r} value={r}>{r}</option>
                                    ))}
                                </optgroup>
                            </select>

                            {raceError && <p className="mt-1 text-xs text-red-700">{raceError}</p>}

                            {addingRace && (
                                <div className="mt-2 rounded-lg border border-stone-300 bg-amber-50 p-3">
                                    <p className="text-xs text-stone-600 mb-2">
                                        Add a homebrew race to this campaign’s roster.
                                    </p>

                                    <div className="flex gap-2">
                                        <input
                                            value={newRaceName}
                                            onChange={(e) => setNewRaceName(e.target.value)}
                                            placeholder="e.g. Aasimar"
                                            className="flex-1 border border-stone-400 rounded-md p-2 bg-white"
                                        />
                                        <button
                                            type="button"
                                            onClick={handleAddRace}
                                            className="px-3 py-2 text-sm font-semibold rounded-lg bg-emerald-700 text-white hover:bg-emerald-600"
                                        >
                                            Inscribe
                                        </button>
                                    </div>
                                </div>
                            )}
                        </div>

                        {/* Background + Homebrew */}
                        <div>
                            <div className="flex items-center justify-between">
                                <label className="block text-sm font-semibold text-stone-700">
                                    Background
                                </label>

                                <button
                                    type="button"
                                    className="text-xs font-semibold text-emerald-800 hover:underline"
                                    onClick={() => setAddingBackground((v) => !v)}
                                >
                                    {addingBackground ? "Close" : "Add homebrew"}
                                </button>
                            </div>

                            <select
                                name="background"
                                value={form.background}
                                onChange={handleChange}
                                className="w-full border border-stone-400 rounded-md p-2 bg-white"
                                disabled={backgroundLoading}
                            >
                                <option value="" disabled>
                                    {backgroundLoading ? "Summoning backgrounds..." : "Choose a background"}
                                </option>

                                <optgroup label="Official">
                                    {backgroundNames
                                        .filter((b) => isOfficialBackground(b))
                                        .map((b) => (
                                            <option key={b} value={b}>
                                                {b}
                                            </option>
                                        ))}
                                </optgroup>

                                <optgroup label="Homebrew">
                                    {backgroundNames
                                        .filter((b) => !isOfficialBackground(b))
                                        .map((b) => (
                                            <option key={b} value={b}>
                                                {b}
                                            </option>
                                        ))}
                                </optgroup>
                            </select>

                            {backgroundError && <p className="mt-1 text-xs text-red-700">{backgroundError}</p>}

                            {addingBackground && (
                                <div className="mt-2 rounded-lg border border-stone-300 bg-amber-50 p-3">
                                    <p className="text-xs text-stone-600 mb-2">
                                        Add a homebrew background to this campaign’s roster.
                                    </p>

                                    <div className="flex gap-2">
                                        <input
                                            value={newBackgroundName}
                                            onChange={(e) => setNewBackgroundName(e.target.value)}
                                            placeholder="e.g. Haunted One"
                                            className="flex-1 border border-stone-400 rounded-md p-2 bg-white"
                                        />
                                        <button
                                            type="button"
                                            onClick={handleAddBackground}
                                            className="px-3 py-2 text-sm font-semibold rounded-lg bg-emerald-700 text-white hover:bg-emerald-600"
                                        >
                                            Inscribe
                                        </button>
                                    </div>
                                </div>
                            )}
                        </div>

                        {/* Alignment */}
                        <div>
                            <label className="block text-sm font-semibold text-stone-700">
                                Alignment
                            </label>

                            <select
                                name="alignment"
                                value={form.alignment}
                                onChange={(e) =>
                                    setForm((prev) => ({ ...prev, alignment: e.target.value }))
                                }
                                className="w-full border border-stone-400 rounded-md p-2 bg-white"
                            >
                                <option value="">Choose alignment</option>
                                {ALIGNMENT_OPTIONS.map((a) => (
                                    <option key={a} value={a}>
                                        {a}
                                    </option>
                                ))}
                            </select>

                            <p className="mt-1 text-xs text-stone-500">
                                “Neutral” = True Neutral.
                            </p>
                        </div>

                        {/* XP */}
                        <div>
                            <label className="block text-sm font-semibold text-stone-700">XP</label>
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
                                    min={1}
                                    max={30}
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

                {/* Hit Dice */}
                <section className={sectionClass}>
                    <h2 className={sectionTitleClass}>Hit Dice</h2>

                    <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
                        <div>
                            <label className="block text-sm font-semibold text-stone-700">Die</label>
                            <input
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

                {/* Attacks */}
                <section className={sectionClass}>
                    <div className="flex items-center justify-between mb-4">
                        <h2 className="text-lg font-semibold text-stone-700">Attacks & Spellcasting</h2>

                        <button
                            type="button"
                            onClick={() =>
                                setForm((prev) => ({
                                    ...prev,
                                    attacks: [
                                        ...(prev.attacks ?? []),
                                        {
                                            id: null,
                                            clientId: crypto.randomUUID(),
                                            name: "",
                                            attackBonus: 0,
                                            damage: "",
                                        },
                                    ],
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
                                    key={a.clientId}
                                    className="grid grid-cols-1 md:grid-cols-12 gap-3 border border-stone-200 rounded-md bg-white p-3"
                                >
                                    <div className="md:col-span-5">
                                        <label className="block text-xs font-semibold text-stone-600">Name</label>
                                        <input
                                            value={a.name}
                                            onChange={(e) => {
                                                const v = e.target.value;
                                                setForm((prev) => {
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
                                            onChange={(e) => {
                                                const v = Number(e.target.value);
                                                setForm((prev) => {
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
                                            onChange={(e) => {
                                                const v = e.target.value;
                                                setForm((prev) => {
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
                                                    setForm((prev) => ({
                                                        ...prev,
                                                        attacks: (prev.attacks ?? []).filter(
                                                            (x) => x.clientId !== a.clientId
                                                        ),
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
                    <Button
                        type="button"
                        variant="secondary"
                        onClick={onCancel}
                        disabled={submitting}
                    >
                        Cancel
                    </Button>

                    <Button type="submit" variant="primary" disabled={submitting}>
                        {submitting ? "Saving..." : submitLabel}
                    </Button>
                </div>
            </form>
        </FormCard>
    );

    // -------------------- READ VIEW --------------------
    const ReadView = (
        <FormCard
            title={title}
            subtitle={subtitle}
            topRight={
                <Button type="button" variant="ghost" onClick={() => setIsReadView(false)}>
                    Edit
                </Button>
            }
        >
            {error && <ErrorPanel message={error} />}

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
        </FormCard>
    );

    return isReadView ? ReadView : EditView;
}

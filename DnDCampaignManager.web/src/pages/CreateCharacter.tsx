import { useState } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { createCharacter } from "../api/campaignApi";
import { extractApiError } from "../Utils/apiError";
import FormCard from "../components/UI/FormCard";

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

type CharacterSkill = {
    skill: SkillType;
    ability: AbilityType;
    isProficient: boolean;
    isExpertise: boolean;
    miscBonus: number;
};

type CharacterForm = {
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

    skills: CharacterSkill[];
};

const emptyForm: CharacterForm = {
    name: "",
    class: "",
    race: "",
    level: 1,
    background: "",
    alignment: "",
    experiencePoints: 0,

    armorClass: 10,
    initiative: 0,
    speed: 30,

    hitPointMax: 1,
    hitPointCurrent: 1,
    hitPointTemporary: 0,

    strength: 10,
    dexterity: 10,
    constitution: 10,
    intelligence: 10,
    wisdom: 10,
    charisma: 10,

    skills: []
};

export default function CreateCharacter() {
    const { id } = useParams(); // campaignId param
    const navigate = useNavigate();

    const [form, setForm] = useState<CharacterForm>(emptyForm);
    const [submitting, setSubmitting] = useState(false);
    const [error, setError] = useState<string | null>(null);

    const handleChange = (
        e: React.ChangeEvent<HTMLInputElement | HTMLTextAreaElement>
    ) => {
        setError(null);

        const { name, value } = e.target;
        const isNumber =
            e.target instanceof HTMLInputElement && e.target.type === "number";

        setForm(prev => ({
            ...prev,
            [name]: isNumber ? Number(value) : value
        }));
    };

    const submit = async (e: React.FormEvent) => {
        e.preventDefault();
        if (!id) return;

        setError(null);
        setSubmitting(true);

        try {
            await createCharacter(Number(id), form);
            navigate("/dashboard");
        } catch (err: any) {
            setError(extractApiError(err, "Failed to create character."));
        } finally {
            setSubmitting(false);
        }
    };

    const abilityAbbrev = (a: AbilityType) =>
    ({
        Strength: "STR",
        Dexterity: "DEX",
        Constitution: "CON",
        Intelligence: "INT",
        Wisdom: "WIS",
        Charisma: "CHA"
    }[a]);

    const prettySkill = (s: string) => s.replace(/([A-Z])/g, " $1").trim();

    return (
        <FormCard
            title="🧙 Create Character"
            subtitle="Fill out the essentials (5e sheet – page 1)."
            error={error}
            backTo="/dashboard"
        >
            <form onSubmit={submit} className="space-y-6">
                {/* Identity */}
                <section className="bg-stone-50 border border-stone-300 rounded-lg p-4">
                    <h2 className="text-lg font-semibold text-stone-700 mb-4">Identity</h2>

                    <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
                        <div className="md:col-span-2">
                            <label className="block text-sm font-semibold text-stone-700">Name</label>
                            <input
                                name="name"
                                value={form.name}
                                onChange={handleChange}
                                className="w-full border border-stone-400 rounded-md p-2 bg-white"
                                required
                            />
                        </div>

                        <div>
                            <label className="block text-sm font-semibold text-stone-700">Level</label>
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
                            <label className="block text-sm font-semibold text-stone-700">Class</label>
                            <input
                                name="class"
                                value={form.class}
                                onChange={handleChange}
                                className="w-full border border-stone-400 rounded-md p-2 bg-white"
                                required
                            />
                        </div>

                        <div>
                            <label className="block text-sm font-semibold text-stone-700">Race</label>
                            <input
                                name="race"
                                value={form.race}
                                onChange={handleChange}
                                className="w-full border border-stone-400 rounded-md p-2 bg-white"
                                required
                            />
                        </div>

                        <div>
                            <label className="block text-sm font-semibold text-stone-700">Background</label>
                            <input
                                name="background"
                                value={form.background}
                                onChange={handleChange}
                                className="w-full border border-stone-400 rounded-md p-2 bg-white"
                            />
                        </div>

                        <div>
                            <label className="block text-sm font-semibold text-stone-700">Alignment</label>
                            <input
                                name="alignment"
                                value={form.alignment}
                                onChange={handleChange}
                                className="w-full border border-stone-400 rounded-md p-2 bg-white"
                            />
                        </div>

                        <div>
                            <label className="block text-sm font-semibold text-stone-700">
                                Experience Points (XP)
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

                {/* Abilities + Skills + Combat */}
                <section className="grid grid-cols-1 md:grid-cols-3 gap-6">
                    {/* Ability Scores */}
                    <div className="bg-stone-50 border border-stone-300 rounded-lg p-4">
                        <h2 className="text-lg font-semibold text-stone-700 mb-4">Ability Scores</h2>

                        {(
                            [
                                ["strength", "STR"],
                                ["dexterity", "DEX"],
                                ["constitution", "CON"],
                                ["intelligence", "INT"],
                                ["wisdom", "WIS"],
                                ["charisma", "CHA"]
                            ] as const
                        ).map(([key, label]) => (
                            <div key={key} className="flex items-center justify-between mb-3">
                                <label className="uppercase text-sm font-bold text-stone-600">{label}</label>

                                <input
                                    type="number"
                                    name={key}
                                    min={1}
                                    max={30}
                                    value={(form as any)[key]}
                                    onChange={handleChange}
                                    className="w-24 text-center border border-stone-400 rounded-md p-1 bg-white"
                                />
                            </div>
                        ))}
                    </div>

                    {/* Skills */}
                    <section className="bg-stone-50 border border-stone-300 rounded-lg p-4 md:col-span-2">
                        <h2 className="text-lg font-semibold text-stone-700 mb-4">Skills</h2>

                        {(!form.skills || form.skills.length === 0) ? (
                            <p className="text-stone-600 text-sm">No skills loaded yet.</p>
                        ) : (
                            <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
                                {form.skills.map((sk, idx) => (
                                    <div
                                        key={sk.skill}
                                        className="flex items-center justify-between border border-stone-200 rounded-md bg-white px-3 py-2"
                                    >
                                        <div>
                                            <div className="font-semibold text-stone-800">
                                                {prettySkill(sk.skill)}
                                            </div>
                                            <div className="text-xs text-stone-500">
                                                {abilityAbbrev(sk.ability)}
                                            </div>
                                        </div>

                                        <div className="flex items-center gap-2">
                                            <label className="text-xs text-stone-600">Prof</label>
                                            <input
                                                type="checkbox"
                                                checked={sk.isProficient}
                                                onChange={e => {
                                                    setError(null);
                                                    const checked = e.target.checked;
                                                    setForm(prev => {
                                                        const copy = [...prev.skills];
                                                        copy[idx] = { ...copy[idx], isProficient: checked };
                                                        if (!checked) copy[idx].isExpertise = false;
                                                        return { ...prev, skills: copy };
                                                    });
                                                }}
                                            />

                                            <label className="text-xs text-stone-600">Exp</label>
                                            <input
                                                type="checkbox"
                                                disabled={!sk.isProficient}
                                                checked={sk.isExpertise}
                                                onChange={e => {
                                                    setError(null);
                                                    const checked = e.target.checked;
                                                    setForm(prev => {
                                                        const copy = [...prev.skills];
                                                        copy[idx] = { ...copy[idx], isExpertise: checked };
                                                        return { ...prev, skills: copy };
                                                    });
                                                }}
                                            />
                                        </div>
                                    </div>
                                ))}
                            </div>
                        )}
                    </section>

                    {/* Combat + HP */}
                    <div className="md:col-span-2 bg-stone-50 border border-stone-300 rounded-lg p-4">
                        <h2 className="text-lg font-semibold text-stone-700 mb-4">Combat</h2>

                        <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
                            <div>
                                <label className="block text-sm font-semibold text-stone-700">Armor Class</label>
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
                                <label className="block text-sm font-semibold text-stone-700">Initiative</label>
                                <input
                                    type="number"
                                    name="initiative"
                                    value={form.initiative}
                                    onChange={handleChange}
                                    className="w-full border border-stone-400 rounded-md p-2 bg-white"
                                />
                            </div>

                            <div>
                                <label className="block text-sm font-semibold text-stone-700">Speed</label>
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
                            <h3 className="text-md font-semibold text-stone-700 mb-3">Hit Points</h3>

                            <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
                                <div>
                                    <label className="block text-sm font-semibold text-stone-700">Max HP</label>
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
                                    <label className="block text-sm font-semibold text-stone-700">Current HP</label>
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
                                    <label className="block text-sm font-semibold text-stone-700">Temp HP</label>
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
                    </div>
                </section>

                {/* Actions */}
                <div className="flex justify-end gap-3">
                    <button
                        type="button"
                        onClick={() => navigate("/dashboard")}
                        className="px-4 py-2 rounded-md border border-stone-400 bg-stone-200 hover:bg-stone-300 disabled:opacity-60"
                        disabled={submitting}
                    >
                        Cancel
                    </button>

                    <button
                        type="submit"
                        className="px-5 py-2 rounded-md bg-emerald-700 text-white font-semibold hover:bg-emerald-600 disabled:opacity-60"
                        disabled={submitting}
                    >
                        {submitting ? "Creating..." : "Create Character"}
                    </button>
                </div>
            </form>

        </FormCard>
    );
}

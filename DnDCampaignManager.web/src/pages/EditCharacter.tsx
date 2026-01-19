import { useEffect, useState } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { getCharacter, updateCharacterByCampaign } from "../api/campaignApi";
import { extractApiError } from "../Utils/apiError";
import FormCard from "../components/UI/FormCard";

type CharacterForm = {
    // Identity
    name: string;
    class: string;
    race: string;
    level: number;
    background: string;
    alignment: string;
    experiencePoints: number;

    // Combat basics
    armorClass: number;
    initiative: number;
    speed: number;

    // HP
    hitPointMax: number;
    hitPointCurrent: number;
    hitPointTemporary: number;

    // Ability scores
    strength: number;
    dexterity: number;
    constitution: number;
    intelligence: number;
    wisdom: number;
    charisma: number;

    skills: CharacterSkill[];
};

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
export default function EditCharacter() {
    const { campaignId, characterId } = useParams();
    const navigate = useNavigate();

    const [form, setForm] = useState<CharacterForm>(emptyForm);
    const [loading, setLoading] = useState(true);

    const [submitting, setSubmitting] = useState(false);
    const [error, setError] = useState<string | null>(null);

    // Handles <input> + <textarea>
    const handleChange = (
        e: React.ChangeEvent<HTMLInputElement | HTMLTextAreaElement>
    ) => {
        const { name, value } = e.target;
        const isNumber =
            e.target instanceof HTMLInputElement && e.target.type === "number";

        setError(null);
        setForm(prev => ({
            ...prev,
            [name]: isNumber ? Number(value) : value
        }));
    };

    const abilityAbbrev = (a: AbilityType) =>
        ({ Strength: "STR", Dexterity: "DEX", Constitution: "CON", Intelligence: "INT", Wisdom: "WIS", Charisma: "CHA" }[a]);

    const prettySkill = (s: string) =>
        s.replace(/([A-Z])/g, " $1").trim();

    const proficiencyFromLevel = (level: number) =>
        2 + Math.floor((Math.max(1, level) - 1) / 4);

    const abilityMod = (score: number) => Math.floor((score - 10) / 2);

    const abilityScoreFor = (ability: AbilityType, form: any) => {
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

    const formatMod = (n: number) => (n >= 0 ? `+${n}` : `${n}`);

    const calcSkillMod = (sk: CharacterSkill, form: any) => {
        const pb = proficiencyFromLevel(form.level);
        const mod = abilityMod(abilityScoreFor(sk.ability, form));
        const prof = sk.isProficient ? pb : 0;
        const exp = sk.isExpertise ? pb : 0; // expertise adds another PB
        const misc = sk.miscBonus ?? 0;
        return mod + prof + exp + misc;
    };

    useEffect(() => {
        const load = async () => {
            if (!campaignId || !characterId) return;

            try {
                const res = await getCharacter(Number(campaignId), Number(characterId));

                // If backend returns extra fields, this will populate them.
                // If some fields are missing, we fallback to emptyForm defaults.
                setForm({
                    ...emptyForm,
                    ...res.data
                });
            } catch (err: any) {
                alert(err?.response?.data ?? "Character not found");
                navigate("/dashboard");
            } finally {
                setLoading(false);
            }
        };

        load();
    }, [campaignId, characterId, navigate]);

    const submit = async (e: React.FormEvent) => {
        e.preventDefault();
        if (!campaignId || !characterId) return;

        setError(null);
        setSubmitting(true);

        try {
            await updateCharacterByCampaign(
                Number(campaignId),
                Number(characterId),
                form
            );
            navigate("/dashboard");
        } catch (err: any) {
            setError(extractApiError(err, "Failed to save character."));
        } finally {
            setSubmitting(false);
        }
    };

    if (loading) return <p className="p-6 text-stone-600">Loading...</p>;

    return (
        <FormCard
            title="🧙 Character Sheet"
            subtitle="Update the essentials from your D&D 5e page 1."
            error={error}
            backTo="/dashboard"
        >
                <form onSubmit={submit} className="space-y-6">
                    {/* Top Identity */}
                    <section className="bg-stone-50 border border-stone-300 rounded-lg p-4">
                        <h2 className="text-lg font-semibold text-stone-700 mb-4">
                            Identity
                        </h2>

                        <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
                            <div className="md:col-span-2">
                                <label className="block text-sm font-semibold text-stone-700">
                                    Name
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

                    {/* Mid — Abilities + Combat */}
                    <section className="grid grid-cols-1 md:grid-cols-3 gap-6">
                        {/* Abilities */}
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
                                    ["charisma", "CHA"]
                                ] as const
                            ).map(([key, label]) => (
                                <div
                                    key={key}
                                    className="flex items-center justify-between mb-3"
                                >
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

                        {/* SKills */}
                        <section className="bg-stone-50 border border-stone-300 rounded-lg p-4 md:col-span-2">
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
                                        const mod = calcSkillMod(sk, form);

                                        return (
                                            <div
                                                key={sk.skill}
                                                className="flex items-center justify-between border border-stone-200 rounded-md bg-white px-3 py-2"
                                            >
                                                {/* Left: name + ability */}
                                                <div className="min-w-0">
                                                    <div className="font-semibold text-stone-800 truncate">
                                                        {prettySkill(String(sk.skill))}
                                                    </div>
                                                    <div className="text-xs text-stone-500">
                                                        {abilityAbbrev(sk.ability)}
                                                    </div>
                                                </div>

                                                {/* Right: value + toggles + misc */}
                                                <div className="flex items-center gap-3">
                                                    {/* Calculated modifier */}
                                                    <div className="w-10 text-center font-bold text-stone-800">
                                                        {formatMod(mod)}
                                                    </div>

                                                    {/* Proficiency */}
                                                    <label className="text-xs text-stone-600 flex items-center gap-1">
                                                        Prof
                                                        <input
                                                            type="checkbox"
                                                            checked={!!sk.isProficient}
                                                            onChange={e => {
                                                                const checked = e.target.checked;

                                                                setForm(prev => {
                                                                    const copy = [...prev.skills];
                                                                    const current = copy[idx];

                                                                    copy[idx] = {
                                                                        ...current,
                                                                        isProficient: checked,
                                                                        // if prof off, expertise must be off too
                                                                        isExpertise: checked ? current.isExpertise : false,
                                                                    };

                                                                    return { ...prev, skills: copy };
                                                                });
                                                            }}
                                                        />
                                                    </label>

                                                    {/* Expertise */}
                                                    <label className="text-xs text-stone-600 flex items-center gap-1">
                                                        Exp
                                                        <input
                                                            type="checkbox"
                                                            disabled={!sk.isProficient}
                                                            checked={!!sk.isExpertise}
                                                            onChange={e => {
                                                                const checked = e.target.checked;

                                                                setForm(prev => {
                                                                    const copy = [...prev.skills];
                                                                    copy[idx] = { ...copy[idx], isExpertise: checked };
                                                                    return { ...prev, skills: copy };
                                                                });
                                                            }}
                                                        />
                                                    </label>

                                                    {/* Misc bonus */}
                                                    <input
                                                        type="number"
                                                        value={sk.miscBonus ?? 0}
                                                        onChange={e => {
                                                            const v = Number(e.target.value);

                                                            setForm(prev => {
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
                        </section>


                        {/* Combat */}
                        <div className="md:col-span-2 bg-stone-50 border border-stone-300 rounded-lg p-4">
                            <h2 className="text-lg font-semibold text-stone-700 mb-4">
                                Combat
                            </h2>

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
                        </div>
                    </section>

                    {/* Actions */}
                    <div className="flex justify-end gap-3">
                        <button
                            type="button"
                            onClick={() => navigate("/dashboard")}
                            className="px-4 py-2 rounded-md border border-stone-400 bg-stone-200 hover:bg-stone-300"
                        >
                            Cancel
                        </button>

                        <button
                            type="submit"
                            className="px-5 py-2 rounded-md bg-emerald-700 text-white font-semibold hover:bg-emerald-600"
                        >
                            Save Character
                        </button>
                    </div>
                </form>
        </FormCard>
    );
}

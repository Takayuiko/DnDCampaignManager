import { useEffect, useMemo, useState } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { getCharacter, updateCharacterByCampaign } from "../api/campaignApi";
import CharacterSheetForm, {
    type CharacterForm,
    type CharacterSkill
} from "../components/Character/CharacterSheetForm";
import Button from "../components/UI/Button";
import { extractApiError } from "../Utils/apiError";

const defaultSkills: CharacterSkill[] = [
    { skill: "Acrobatics", ability: "Dexterity", isProficient: false, isExpertise: false, miscBonus: 0 },
    { skill: "AnimalHandling", ability: "Wisdom", isProficient: false, isExpertise: false, miscBonus: 0 },
    { skill: "Arcana", ability: "Intelligence", isProficient: false, isExpertise: false, miscBonus: 0 },
    { skill: "Athletics", ability: "Strength", isProficient: false, isExpertise: false, miscBonus: 0 },
    { skill: "Deception", ability: "Charisma", isProficient: false, isExpertise: false, miscBonus: 0 },
    { skill: "History", ability: "Intelligence", isProficient: false, isExpertise: false, miscBonus: 0 },
    { skill: "Insight", ability: "Wisdom", isProficient: false, isExpertise: false, miscBonus: 0 },
    { skill: "Intimidation", ability: "Charisma", isProficient: false, isExpertise: false, miscBonus: 0 },
    { skill: "Investigation", ability: "Intelligence", isProficient: false, isExpertise: false, miscBonus: 0 },
    { skill: "Medicine", ability: "Wisdom", isProficient: false, isExpertise: false, miscBonus: 0 },
    { skill: "Nature", ability: "Intelligence", isProficient: false, isExpertise: false, miscBonus: 0 },
    { skill: "Perception", ability: "Wisdom", isProficient: false, isExpertise: false, miscBonus: 0 },
    { skill: "Performance", ability: "Charisma", isProficient: false, isExpertise: false, miscBonus: 0 },
    { skill: "Persuasion", ability: "Charisma", isProficient: false, isExpertise: false, miscBonus: 0 },
    { skill: "Religion", ability: "Intelligence", isProficient: false, isExpertise: false, miscBonus: 0 },
    { skill: "SleightOfHand", ability: "Dexterity", isProficient: false, isExpertise: false, miscBonus: 0 },
    { skill: "Stealth", ability: "Dexterity", isProficient: false, isExpertise: false, miscBonus: 0 },
    { skill: "Survival", ability: "Wisdom", isProficient: false, isExpertise: false, miscBonus: 0 },
];

const emptyForm: CharacterForm = {
    campaignId: 0,
    // Identity
    name: "",
    class: "",
    race: "",
    level: 1,
    background: "",
    alignment: "",
    experiencePoints: 0,

    // Combat
    armorClass: 10,
    initiative: 0,
    speed: 30,

    // HP
    hitPointMax: 1,
    hitPointCurrent: 1,
    hitPointTemporary: 0,

    // Abilities
    strength: 10,
    dexterity: 10,
    constitution: 10,
    intelligence: 10,
    wisdom: 10,
    charisma: 10,

    inspiration: false,

    // Saving Throws
    savingThrows: {
        strength: { isProficient: false, miscBonus: 0 },
        dexterity: { isProficient: false, miscBonus: 0 },
        constitution: { isProficient: false, miscBonus: 0 },
        intelligence: { isProficient: false, miscBonus: 0 },
        wisdom: { isProficient: false, miscBonus: 0 },
        charisma: { isProficient: false, miscBonus: 0 },
    },

    // Temporal
    hitDice: {
        die: "d8",
        total: 1,
        remaining: 1
    },

    attacks: [],
    skills: defaultSkills,
};

export default function EditCharacter() {
    const { campaignId, characterId } = useParams();
    const cid = Number(campaignId);
    const chid = Number(characterId);

    const navigate = useNavigate();

    const [form, setForm] = useState<CharacterForm>(emptyForm);
    const [loading, setLoading] = useState(true);

    const [submitting, setSubmitting] = useState(false);
    const [error, setError] = useState<string | null>(null);

    const topRight = useMemo(
        () => (
            <Button variant="subtle" type="button" onClick={() => navigate("/dashboard")}>
                Back
            </Button>
        ),
        [navigate]
    );

    useEffect(() => {
        const load = async () => {
            if (!cid || !chid) return;

            setError(null);
            setLoading(true);
            try {
                const res = await getCharacter(cid, chid);

                const character = res.data;

                setForm({
                    ...emptyForm,
                    ...character,
                    campaignId: cid,
                    skills:
                        Array.isArray(character.skills) && character.skills.length > 0
                            ? character.skills
                            : emptyForm.skills,
                    hitDice: {
                        ...emptyForm.hitDice,
                        ...(character.hitDice ?? {}),
                    },
                    attacks: Array.isArray(character.attacks)
                        ? character.attacks.map(attack => ({ ...attack, clientId: crypto.randomUUID() }))
                        : [],
                });
            } catch (err: any) {
                alert(err?.response?.data ?? "Character not found");
                navigate("/dashboard");
            } finally {
                setLoading(false);
            }
        };

        load();
    }, [cid, chid, navigate]);

    const submit = async (e: React.FormEvent) => {
        e.preventDefault();
        if (!cid || !chid) return;

        setError(null);
        setSubmitting(true);
        try {
            await updateCharacterByCampaign(cid, chid, form);
            alert("Character stats updated")
        } catch (err: any) {
            setError(extractApiError(err, "Failed to save character."));
        } finally {
            setSubmitting(false);
        }
    };

    if (loading) return <p className="p-6 text-stone-600">Loading...</p>;

    return (
        <CharacterSheetForm
            title="🧙 Character Sheet"
            subtitle="Update the essentials from your D&D 5e page 1."
            topRight={topRight}
            form={form}
            setForm={setForm}
            onCancel={() => navigate("/dashboard")}
            onSubmit={submit}
            submitLabel="Save Character"
            submitting={submitting}
            error={error}
        />
    );
}

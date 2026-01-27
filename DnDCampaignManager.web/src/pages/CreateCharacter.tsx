    import { useMemo, useState, useEffect } from "react";
    import { useNavigate, useParams } from "react-router-dom";
    import { createCharacter } from "../api/campaignApi";
    import CharacterSheetForm, {
        type CharacterForm,
        type CharacterSkill,
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
    inspiration: false,
    savingThrows: {
        strength: { isProficient: false, miscBonus: 0 },
        dexterity: { isProficient: false, miscBonus: 0 },
        constitution: { isProficient: false, miscBonus: 0 },
        intelligence: { isProficient: false, miscBonus: 0 },
        wisdom: { isProficient: false, miscBonus: 0 },
        charisma: { isProficient: false, miscBonus: 0 },
    },
    hitDice: { die: "d8", total: 1, remaining: 1 },
    attacks: [],
    skills: defaultSkills,
};

export default function CreateCharacter() {
    const navigate = useNavigate();
    const { campaignId } = useParams<{ campaignId: string }>();

    const campaignIdParam = Number(campaignId);
    const hasValidCampaignId = Number.isFinite(campaignIdParam) && campaignIdParam > 0;

    const [form, setForm] = useState<CharacterForm>(() => ({
        ...emptyForm,
        campaignId: hasValidCampaignId ? campaignIdParam : 0,
    }));

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
        if (!hasValidCampaignId) return;
        setForm(prev => ({ ...prev, campaignId: campaignIdParam }));
    }, [hasValidCampaignId, campaignIdParam]);

    const submit = async (e: React.FormEvent) => {
        e.preventDefault();
        if (!hasValidCampaignId) {
            setError("Invalid campaign id in URL.");
            return;
        }

        setError(null);
        setSubmitting(true);
        try {
            await createCharacter(campaignIdParam, form);
            navigate("/dashboard");
        } catch (err: any) {
            setError(extractApiError(err, "Failed to create character."));
        } finally {
            setSubmitting(false);
        }
    };

    if (!hasValidCampaignId) {
        return (
            <div className="p-6 text-stone-700">
                <p className="mb-3 font-semibold">Missing/invalid campaign id in URL.</p>
                <Button variant="subtle" type="button" onClick={() => navigate("/dashboard")}>
                    Back to dashboard
                </Button>
            </div>
        );
    }

    return (
        <CharacterSheetForm
            title="🧙 Create Character"
            subtitle="Fill out the essentials (5e sheet – page 1)."
            topRight={topRight}
            form={form}
            setForm={setForm}
            onCancel={() => navigate("/dashboard")}
            onSubmit={submit}
            submitLabel="Create Character"
            submitting={submitting}
            error={error}
        />
    );
}

    

import { abilityModUtil, formatModUtil } from "../../Utils/dnd";
import type { AbilityType, CharacterForm, CharacterSkill } from "./characterTypes";
import { SAVING_THROW_ROWS } from "./characterConstants";

type SavingThrowKey = typeof SAVING_THROW_ROWS[number]["key"];
export function characterCalculations(form: CharacterForm) {
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

    const abilities = [
        { key: "Strength", score: form.strength },
        { key: "Dexterity", score: form.dexterity },
        { key: "Constitution", score: form.constitution },
        { key: "Intelligence", score: form.intelligence },
        { key: "Wisdom", score: form.wisdom },
        { key: "Charisma", score: form.charisma },
    ];

    const pb = proficiencyFromLevel(form.level);

    return { prettySkill, proficiencyFromLevel, abilityMod, formatMod, savingThrowTotal,
        passivePerception, calcSkillMod, abilities, pb };
}

import type { ChangeEvent, Dispatch, SetStateAction } from "react";

export type CharacterOption = { id: number; name: string; isCustom: boolean };

export type SkillType =
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

export type AbilityType =
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

export type CharacterOptionsPayload = {
    classes: CharacterOption[];
    races: CharacterOption[];
    backgrounds: CharacterOption[];
};

export type GetCharacterResponse = Omit<CharacterForm, "campaignId" | "attacks" | "version"> & {
    id: number;
    userId: number;
    version: string;
    proficiencyBonus: number;
    attacks: Omit<Attack, "clientId">[];
};

type SavingThrow = {
    isProficient: boolean;
    miscBonus: number;
};

type HitDice = {
    die: string;
    total: number;
    remaining: number;
};

export type Attack = {
    id?: number | null;
    clientId: string;
    name: string;
    attackBonus: number;
    damage: string;
};

export type CharacterForm = {
    version?: string;
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

export type CharacterFormSetter = Dispatch<SetStateAction<CharacterForm>>;
export type CharacterFieldChange = (event: ChangeEvent<HTMLInputElement | HTMLTextAreaElement | HTMLSelectElement>) => void;
export type CharacterSectionProps = { form: CharacterForm; setForm: CharacterFormSetter };

export type AttackDraft = Omit<Attack, "clientId"> & { clientId?: string };

import type { AbilityType } from "./characterTypes";

export const SAVING_THROW_ROWS = [
    { key: "strength", label: "STR" },
    { key: "dexterity", label: "DEX" },
    { key: "constitution", label: "CON" },
    { key: "intelligence", label: "INT" },
    { key: "wisdom", label: "WIS" },
    { key: "charisma", label: "CHA" },
] as const;

export const CLASS_OPTIONS = [
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

export const ALIGNMENT_OPTIONS = [
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

export const RACE_OPTIONS = [
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

export const BACKGROUND_OPTIONS = [
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

export const abilityAbbrev = (a: AbilityType) =>
({
    Strength: "STR",
    Dexterity: "DEX",
    Constitution: "CON",
    Intelligence: "INT",
    Wisdom: "WIS",
    Charisma: "CHA",
}[a]);

export const isOfficialClass = (name: string) =>
    CLASS_OPTIONS.some((x) => x.toLowerCase() === name.toLowerCase());

export const isOfficialRace = (name: string) =>
    RACE_OPTIONS.some(r => r.toLowerCase() === name.toLowerCase());

export const isOfficialBackground = (name: string) =>
    BACKGROUND_OPTIONS.some(r => r.toLowerCase() === name.toLowerCase())


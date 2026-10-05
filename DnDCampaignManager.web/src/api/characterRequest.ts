import type { CharacterForm, GetCharacterResponse } from "../components/Character/CharacterSheetForm";

// Ownership is determined by the authenticated user and route on the server.
export type CharacterRequest = Omit<GetCharacterResponse, "id" | "userId">;

export function toCharacterRequest(form: CharacterForm): CharacterRequest {
    return {
        name: form.name,
        class: form.class,
        race: form.race,
        level: form.level,
        background: form.background,
        alignment: form.alignment,
        experiencePoints: form.experiencePoints,
        strength: form.strength,
        dexterity: form.dexterity,
        constitution: form.constitution,
        intelligence: form.intelligence,
        wisdom: form.wisdom,
        charisma: form.charisma,
        proficiencyBonus: 2 + Math.floor((form.level - 1) / 4),
        armorClass: form.armorClass,
        initiative: form.initiative,
        speed: form.speed,
        hitPointMax: form.hitPointMax,
        hitPointCurrent: form.hitPointCurrent,
        hitPointTemporary: form.hitPointTemporary,
        inspiration: form.inspiration,
        hitDice: form.hitDice,
        savingThrows: form.savingThrows,
        skills: form.skills,
        attacks: form.attacks.map(attack => ({
            name: attack.name,
            attackBonus: attack.attackBonus,
            damage: attack.damage
        }))
    };
}

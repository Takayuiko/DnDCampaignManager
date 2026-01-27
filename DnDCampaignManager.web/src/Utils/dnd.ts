export function abilityModUtil(score: number): number {
    return Math.floor((score - 10) / 2);
}

export function formatModUtil(mod: number): string {
    return mod >= 0 ? `+${mod}` : `${mod}`;
}
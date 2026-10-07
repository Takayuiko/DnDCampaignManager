import { useEffect, useMemo, useRef, useState } from "react";
import { addCharacterClass, getCharacterClass, addCharacterRace, getCharacterRaces,
    getCharacterBackgrounds, addCharacterBackground } from "../../api/campaignApi";
import { extractApiError } from "../../Utils/apiError";
import { CLASS_OPTIONS } from "./characterConstants";
import type { CharacterFormSetter, CharacterOption, CharacterOptionsPayload } from "./characterTypes";

type OptionKind = "class" | "race" | "background";
const providers = {
    class: { load: getCharacterClass, create: addCharacterClass, plural: "classes" },
    race: { load: getCharacterRaces, create: addCharacterRace, plural: "races" },
    background: { load: getCharacterBackgrounds, create: addCharacterBackground, plural: "backgrounds" }
};

function optionNames(kind: OptionKind, options: CharacterOption[]) {
    const names = kind === "class" ? [...CLASS_OPTIONS, ...options.map(option => option.name)]
        : options.map(option => option.name);
    const unique = new Map<string, string>();
    for (const raw of names) {
        const name = raw.trim();
        if (name && !unique.has(name.toLowerCase())) unique.set(name.toLowerCase(), name);
    }
    return [...unique.values()].sort((a, b) => a.localeCompare(b));
}

// Each group loads independently so a failed request does not hide the other selectors.
function useOptionGroup(kind: OptionKind, campaignId: number, setForm: CharacterFormSetter,
    initialOptions?: CharacterOption[]) {
    const provider = providers[kind];
    const [options, setOptions] = useState<CharacterOption[]>([]);
    const [loading, setLoading] = useState(false);
    const [error, setError] = useState<string | null>(null);
    const [adding, setAdding] = useState(false);
    const [draft, setDraft] = useState("");
    const [saving, setSaving] = useState(false);
    const scope = useRef({ active: false });
    const pendingAdd = useRef(false);

    useEffect(() => {
        const request = { active: true };
        const controller = new AbortController();
        scope.current = request;
        const current = () => request.active;
        setOptions(initialOptions ?? []);
        setError(null);
        setAdding(false);
        setDraft("");
        setSaving(false);
        pendingAdd.current = false;
        setLoading(!!campaignId && !initialOptions);
        if (campaignId && !initialOptions) {
            void provider.load(campaignId, controller.signal).then(response => {
                if (current()) setOptions(response.data);
            }).catch(() => {
                if (current()) setError(`Could not load ${provider.plural} for this campaign.`);
            }).finally(() => {
                if (current()) setLoading(false);
            });
        }
        return () => { request.active = false; controller.abort(); };
    }, [campaignId, initialOptions, provider]);

    async function add() {
        const name = draft.trim();
        if (!name || !campaignId || pendingAdd.current) return;
        const request = scope.current;
        const current = () => request.active;
        pendingAdd.current = true;
        setSaving(true);
        setError(null);
        try {
            const response = await provider.create(campaignId, { name });
            if (!current()) return;
            const added = response.data;
            setOptions(previous => previous.some(option => option.name.toLowerCase() === added.name.toLowerCase())
                ? previous : [...previous, added]);
            setForm(previous => previous.campaignId === campaignId ? { ...previous, [kind]: added.name } : previous);
            setAdding(false);
            setDraft("");
        } catch (requestError: unknown) {
            if (current()) setError(extractApiError(requestError, `Could not add ${kind}.`));
        } finally {
            if (current()) { setSaving(false); pendingAdd.current = false; }
        }
    }

    const names = useMemo(() => options.length || !loading ? optionNames(kind, options) : [], [kind, options, loading]);
    return { names, loading, error, adding, setAdding, draft, setDraft, saving, add };
}

export function useCharacterOptions(campaignId: number, setForm: CharacterFormSetter,
    initialOptions?: CharacterOptionsPayload | null) {
    const classes = useOptionGroup("class", campaignId, setForm, initialOptions?.classes);
    const race = useOptionGroup("race", campaignId, setForm, initialOptions?.races);
    const background = useOptionGroup("background", campaignId, setForm, initialOptions?.backgrounds);
    return { class: classes, race, background };
}

export type CharacterOptionsState = ReturnType<typeof useCharacterOptions>;

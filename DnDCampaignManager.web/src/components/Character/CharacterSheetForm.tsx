import FormCard from "../UI/FormCard";
import Button from "../UI/Button";
import ErrorPanel from "../UI/ErrorPanel";
import CharacterInventory from "./CharacterInventory";
import React, { useState } from "react";
import type { AttackDraft, CharacterForm, CharacterFormSetter, CharacterOptionsPayload } from "./characterTypes";
import { useCharacterOptions } from "./useCharacterOptions";
import CharacterReadSummary from "./CharacterReadSummary";
import IdentitySection from "./IdentitySection";
import AbilitiesSkillsSection from "./AbilitiesSkillsSection";
import SavingThrowsSection from "./SavingThrowsSection";
import CombatSection from "./CombatSection";
import HitDiceSection from "./HitDiceSection";
import CoreSection from "./CoreSection";
import AttacksSection from "./AttacksSection";

export type { CharacterForm, CharacterSkill, CharacterOptionsPayload, GetCharacterResponse } from "./characterTypes";

type Props = {
    title: string;
    subtitle?: string;
    topRight?: React.ReactNode;

    form: CharacterForm;
    setForm: CharacterFormSetter;

    onCancel: () => void;
    onSubmit: (e: React.FormEvent) => void;

    submitLabel: string;
    submitting?: boolean;
    saveDisabled?: boolean;
    error?: string | null;

    initialOptions?: CharacterOptionsPayload | null;
    characterId?: number;
};

export default function CharacterSheetForm({
    title,
    subtitle,
    topRight,
    form,
    setForm,
    onCancel,
    onSubmit,
    submitLabel,
    submitting,
    saveDisabled,
    error,
    initialOptions,
    characterId
}: Props) {
    const handleChange = (
        e: React.ChangeEvent<HTMLInputElement | HTMLTextAreaElement | HTMLSelectElement>
    ) => {
        const { name, value } = e.target;
        const isNumber =
            e.target instanceof HTMLInputElement && e.target.type === "number";

        setForm((prev) => ({
            ...prev,
            [name]: isNumber ? Number(value) : value,
        }));
    };

    const options = useCharacterOptions(form.campaignId, setForm, initialOptions);
    const [isReadView, setIsReadView] = useState(false);
    const [activeTab, setActiveTab] = useState<"character" | "inventory">("character");

    const [attackDraft, setAttackDraft] = useState<AttackDraft | null>(null);

    const EditView = <form onSubmit={event => {
        if (attackDraft || saveDisabled || submitting) { event.preventDefault(); return; }
        onSubmit(event);
    }} className="space-y-6">
        <IdentitySection form={form} setForm={setForm} handleChange={handleChange} options={options} />
        <AbilitiesSkillsSection form={form} setForm={setForm} handleChange={handleChange} />
        <SavingThrowsSection form={form} setForm={setForm} />
        <CombatSection form={form} handleChange={handleChange} />
        <HitDiceSection form={form} setForm={setForm} />
        <CoreSection form={form} setForm={setForm} />
        <AttacksSection form={form} setForm={setForm} draft={attackDraft} setDraft={setAttackDraft} submitting={submitting} />
        <div className="flex justify-end gap-3">
            <Button type="button" variant="secondary" onClick={onCancel} disabled={submitting}>
                Cancel
            </Button>
            <Button type="submit" variant="primary" disabled={submitting || saveDisabled || !!attackDraft}>
                {submitting ? "Saving..." : submitLabel}
            </Button>
        </div>
    </form>;

    return <FormCard title={title} subtitle={subtitle}>
        <div className="mb-6 flex flex-wrap items-center justify-between gap-3 border-b border-stone-300 pb-3">
            {!isReadView && characterId ? <div role="tablist" aria-label="Character sheet sections" className="flex gap-2">
                {(["character", "inventory"] as const).map(tab => <Button
                    key={tab}
                    id={`sheet-tab-${tab}`}
                    type="button"
                    role="tab"
                    aria-selected={activeTab === tab}
                    aria-controls={`sheet-panel-${tab}`}
                    tabIndex={activeTab === tab ? 0 : -1}
                    variant={activeTab === tab ? "primary" : "secondary"}
                    onClick={() => setActiveTab(tab)}
                    onKeyDown={event => {
                        if (!["ArrowLeft", "ArrowRight", "Home", "End"].includes(event.key)) return;
                        event.preventDefault();
                        const next = event.key === "Home" ? "character" : event.key === "End" ? "inventory" :
                            tab === "character" ? "inventory" : "character";
                        setActiveTab(next);
                        event.currentTarget.parentElement?.querySelector<HTMLButtonElement>(`#sheet-tab-${next}`)?.focus();
                    }}
                >{tab === "character" ? "Character" : "Inventory"}</Button>)}
            </div> : <span className="text-sm font-semibold text-stone-600">{isReadView ? "Character & inventory" : "Character"}</span>}
            {topRight}
            <button type="button" onClick={() => setIsReadView(current => !current)}
                className="ml-auto rounded-lg border border-sky-700 bg-sky-700 px-4 py-2 text-sm font-semibold text-white shadow-sm transition hover:bg-sky-800 focus:outline-none focus:ring-2 focus:ring-sky-600 focus:ring-offset-2">
                {isReadView ? "Edit View" : "Read View"}
            </button>
        </div>
        {error && <ErrorPanel message={error} />}
        {isReadView ? <CharacterReadSummary form={form} /> : <div id="sheet-panel-character" role={characterId ? "tabpanel" : undefined}
            aria-labelledby={characterId ? "sheet-tab-character" : undefined} hidden={activeTab !== "character"}>
            {EditView}
        </div>}
        {characterId && <div id="sheet-panel-inventory" role={isReadView ? undefined : "tabpanel"}
            aria-labelledby={isReadView ? undefined : "sheet-tab-inventory"}
            hidden={!isReadView && activeTab !== "inventory"} className={isReadView ? "mt-6" : undefined}>
            <CharacterInventory campaignId={form.campaignId} characterId={characterId} readOnly={isReadView} />
        </div>}
    </FormCard>;
}

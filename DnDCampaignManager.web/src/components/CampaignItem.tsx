import { useAuth } from "../auth/AuthContext";
import { useNavigate } from "react-router-dom";
import Button from "./UI/Button";

type Character = {
    id: number;
    name: string;
    userId: number;
    class?: string;
    race?: string;
    level?: number;
};

type Campaign = {
    id: number;
    name: string;
    description: string;
    characters: Character[];
};

type Props = {
    campaign: Campaign;
    isDM: boolean;
    onEdit: (id: number) => void;
    onDelete: (id: number) => void;
    onAddCharacter: (campaignId: number) => void;
};

export default function CampaignItem({ campaign, isDM, onEdit, onDelete, onAddCharacter }: Props) {
    const { user } = useAuth();
    const navigate = useNavigate();
    const myCharacter = campaign.characters.find(c => c.userId === user?.id);
    const vacantSlots = Math.max(0, 6 - campaign.characters.length);
    const isFull = campaign.characters.length >= 6;

    return (
        <li className="overflow-hidden rounded-2xl border border-stone-300 bg-amber-50 shadow-sm">
            <div className="space-y-6 p-5 sm:p-7">
                <header className="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
                    <div className="min-w-0 space-y-2">
                        <h3 className="break-words text-2xl font-bold text-stone-900">{campaign.name}</h3>
                        <p className="max-w-4xl whitespace-pre-wrap break-words text-sm leading-relaxed text-stone-600">{campaign.description}</p>
                    </div>
                    {isDM && <Button type="button" variant="secondary" className="shrink-0 self-start" onClick={() => onEdit(campaign.id)}>
                        Edit Campaign
                    </Button>}
                </header>

                <nav aria-label={`${campaign.name} tools`} className="flex flex-wrap gap-3">
                    <Button type="button" variant="secondary" onClick={() => navigate(`/campaigns/${campaign.id}/maps`)}>Maps</Button>
                    <Button type="button" variant="secondary" onClick={() => navigate(`/campaigns/${campaign.id}/session-notes`)}>Session Notes</Button>
                    <Button type="button" variant="secondary" onClick={() => navigate(`/campaigns/${campaign.id}/items`)}>Items</Button>
                </nav>

                <section className="space-y-4" aria-label={`${campaign.name} characters`}>
                    <div className="flex flex-wrap items-center justify-between gap-3">
                        <div className="flex items-baseline gap-3">
                            <h4 className="text-lg font-semibold text-stone-800">Characters</h4>
                            <span className="text-sm text-stone-500">{campaign.characters.length} / 6{isFull ? " · Full" : ""}</span>
                        </div>
                        {user && !myCharacter && !isDM && !isFull && <Button type="button" onClick={() => onAddCharacter(campaign.id)}>+ Add Character</Button>}
                    </div>
                    <ul className="grid grid-cols-1 gap-4 md:grid-cols-2 xl:grid-cols-3">
                        {campaign.characters.map(c => {
                            const isMine = c.userId === user?.id;
                            const details = [c.level !== undefined ? `Level ${c.level}` : null, c.race, c.class].filter(Boolean).join(" · ");
                            return <li key={c.id} className={`flex min-h-32 flex-col justify-between gap-4 rounded-xl border p-4 ${isMine ? "border-emerald-300 bg-emerald-50" : "border-stone-200 bg-white"}`}>
                                <div className="min-w-0">
                                    <div className="flex flex-wrap items-center gap-2">
                                        <span className="break-words font-semibold text-stone-900">{c.name}</span>
                                        {isMine && <span className="rounded-full bg-emerald-700 px-2 py-0.5 text-xs text-white">You</span>}
                                    </div>
                                    {details && <p className="mt-1 break-words text-sm text-stone-600">{details}</p>}
                                </div>
                                {(isDM || isMine) && <Button type="button" size="sm" variant="secondary" className="self-start"
                                    onClick={() => navigate(`/campaigns/${campaign.id}/characters/${c.id}/edit`)}>Character Sheet</Button>}
                            </li>;
                        })}
                        {Array.from({ length: vacantSlots }, (_, index) => <li key={`vacant-${index}`}
                            className="flex min-h-32 items-center justify-center rounded-xl border border-dashed border-stone-300 bg-stone-50/60 p-4 text-center text-sm text-stone-500">
                            Available character space
                        </li>)}
                    </ul>
                </section>
            </div>
            {isDM && <footer className="flex justify-end border-t border-stone-200 bg-stone-50 px-5 py-3 sm:px-7">
                <Button type="button" size="sm" variant="danger" onClick={() => {
                    if (confirm(`Delete ${campaign.name} and its campaign data?`)) onDelete(campaign.id);
                }}>Delete Campaign</Button>
            </footer>}
        </li>
    );
}

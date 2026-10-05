import { useAuth } from "../auth/AuthContext";
import { useNavigate } from "react-router-dom";
import Button from "./UI/Button";

type Character = {
    id: number;
    name: string;
    userId: number;
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

export default function CampaignItem({
    campaign,
    isDM,
    onEdit,
    onDelete,
    onAddCharacter
}: Props) {
    const { user } = useAuth();
    const navigate = useNavigate();

    const myCharacter = campaign.characters?.find(c => c.userId === user?.id);

    return (
        <li className="bg-amber-50 border border-stone-300 rounded-xl shadow-sm p-5 flex flex-col gap-4 hover:shadow-md transition">
            <div>
                <h3 className="text-lg font-bold text-stone-800">
                    {campaign.name}
                </h3>
                <p className="text-sm text-stone-600">
                    {campaign.description}
                </p>
            </div>

            {/* Characters */}
            <div className="space-y-2">
                <strong className="text-stone-700">Characters</strong>

                {campaign.characters.length === 0 ? (
                    <p className="text-sm text-stone-500">No characters yet</p>
                ) : (
                    <ul className="space-y-2">
                        {campaign.characters.map(c => {
                            const isMine = c.userId === user?.id;

                            return (
                                <li
                                    key={c.id}
                                    className={`flex items-center justify-between rounded-lg px-3 py-2 border
                ${isMine
                                            ? "bg-emerald-100 border-emerald-300"
                                            : "bg-stone-50 border-stone-200"
                                        }`}
                                >
                                    <div className="flex items-center gap-2">
                                        <span className="font-medium">{c.name}</span>
                                        {isMine && (
                                            <span className="text-xs bg-emerald-600 text-white px-2 py-0.5 rounded">
                                                You
                                            </span>
                                        )}
                                    </div>

                                    <div className="flex flex-wrap gap-2 mt-3">
                                        {(isDM || isMine) && (
                                            <Button size="sm" variant="secondary"
                                                onClick={() => navigate(`/campaigns/${campaign.id}/characters/${c.id}/edit`)}>
                                                Edit
                                            </Button>
                                        )}
                                    </div>
                                </li>
                            );
                        })}
                    </ul>
                )}
            </div>

            {/* Actions */}
            <div className="flex flex-wrap gap-2 mt-3">
                <Button size="sm" variant="secondary" onClick={() => navigate(`/campaigns/${campaign.id}/session-notes`)}>
                    Session Notes
                </Button>
                {isDM && (
                    <>
                        <Button size="sm" variant="secondary"
                            onClick={() => onEdit(campaign.id)}>
                            Edit Campaign
                        </Button>

                        <Button
                            size="sm"
                            variant="danger"
                            onClick={() => {
                                if (confirm("Delete this campaign?")) {
                                    onDelete(campaign.id);
                                }
                            }}
                        >
                            Delete
                        </Button>

                    </>
                )}

                {user && !myCharacter && !isDM && (
                    <button
                        onClick={() => onAddCharacter(campaign.id)}
                        className="ml-auto px-3 py-1 rounded bg-emerald-700 text-white text-sm hover:bg-emerald-600"
                    >
                        + Add Character
                    </button>
                )}
            </div>
        </li>
    );
}

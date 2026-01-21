import type { ReactNode } from "react";
import { useNavigate } from "react-router-dom";
import ErrorPanel from "./ErrorPanel";
import Button from "./Button";

type Props = {
    title: string;
    subtitle?: string;
    backTo?: string; // default "/dashboard"
    error?: string | null;

    /** New name */
    rightActions?: ReactNode;

    /** Old name (kept for compatibility) */
    topRight?: ReactNode;

    children: ReactNode;
};

export default function FormCard({
    title,
    subtitle,
    backTo = "/dashboard",
    error,
    rightActions,
    topRight,
    children,
}: Props) {
    const navigate = useNavigate();

    const actions = rightActions ?? topRight;

    return (
        <div className="min-h-screen bg-stone-100 px-4 py-8">
            <div className="max-w-5xl mx-auto bg-amber-50 border border-stone-300 rounded-xl shadow-lg p-6 space-y-6">
                <div className="flex items-start justify-between gap-4">
                    <div>
                        <h1 className="text-3xl font-bold text-stone-800">{title}</h1>
                        {subtitle && <p className="text-stone-600 text-sm">{subtitle}</p>}
                    </div>

                    <div className="flex items-center gap-2">
                        {actions}
                        <Button type="button" variant="secondary" onClick={() => navigate(backTo)}>
                            Back
                        </Button>
                    </div>
                </div>

                {error && <ErrorPanel message={error} />}

                <div>{children}</div>
            </div>
        </div>
    );
}

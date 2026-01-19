import type { ReactNode } from "react";
import { useNavigate } from "react-router-dom";
import ErrorPanel from "./ErrorPanel";

type Props = {
    title: string;
    subtitle?: string;
    backTo?: string; // default "/dashboard"
    error?: string | null;
    rightActions?: ReactNode; // optional (e.g., extra buttons)
    children: ReactNode; // usually your <form>...</form>
};

export default function FormCard({
    title,
    subtitle,
    backTo = "/dashboard",
    error,
    rightActions,
    children
}: Props) {
    const navigate = useNavigate();

    return (
        <div className="min-h-screen bg-stone-100 px-4 py-8">
            <div className="max-w-5xl mx-auto bg-amber-50 border border-stone-300 rounded-xl shadow-lg p-6 space-y-6">
                <div className="flex items-start justify-between gap-4">
                    <div>
                        <h1 className="text-3xl font-bold text-stone-800">{title}</h1>
                        {subtitle && <p className="text-stone-600 text-sm">{subtitle}</p>}
                    </div>

                    <div className="flex items-center gap-2">
                        {rightActions}
                        <button
                            type="button"
                            onClick={() => navigate(backTo)}
                            className="px-3 py-2 rounded-md border border-stone-300 bg-stone-100 hover:bg-stone-200 text-stone-700"
                        >
                            Back
                        </button>
                    </div>
                </div>

                <ErrorPanel message={error} />

                {children}
            </div>
        </div>
    );
}

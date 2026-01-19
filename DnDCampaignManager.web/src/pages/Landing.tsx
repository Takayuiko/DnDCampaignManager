import { Link, useNavigate } from "react-router-dom";
import { useAuth } from "../auth/AuthContext";
import { useEffect } from "react";
import Button from "../components/UI/Button";

export default function Landing() {
    const { user, loading } = useAuth();
    const navigate = useNavigate();

    // If already logged in, go straight to dashboard
    useEffect(() => {
        if (!loading && user) navigate("/dashboard");
    }, [loading, user, navigate]);

    return (
        <div className="min-h-screen bg-stone-100">
            {/* Background / hero */}
            <div className="relative overflow-hidden">
                <div className="absolute inset-0 bg-gradient-to-b from-stone-900 via-stone-800 to-stone-100" />
                <div className="absolute inset-0 opacity-20 bg-[radial-gradient(circle_at_top,rgba(255,255,255,0.25),transparent_55%)]" />

                <div className="relative mx-auto max-w-6xl px-4 py-10 sm:py-14">
                    <div className="flex flex-col items-center text-center">
                        <div className="inline-flex items-center gap-2 rounded-full bg-amber-100/90 px-3 py-1 text-sm font-semibold text-stone-800 shadow">
                            <span>🎲</span>
                            <span>DnD Campaign Manager</span>
                        </div>

                        <h1 className="mt-5 text-3xl font-extrabold tracking-tight text-stone-50 sm:text-5xl">
                            Organize your campaign like a seasoned DM.
                        </h1>

                        <p className="mt-3 max-w-xl text-base text-stone-200 sm:text-lg">
                            Manage campaigns, invite players, and keep character sheets in one place —
                            fast, simple, and built for tabletop nights.
                        </p>

                        {/* CTA buttons */}
                        <div className="mt-6 flex w-full flex-col gap-3 sm:w-auto sm:flex-row sm:justify-center">
                            <Link
                                to="/login"
                                className="inline-flex items-center justify-center rounded-lg bg-emerald-700 px-5 py-2.5 font-semibold text-white shadow hover:bg-emerald-600"
                            >
                                Login
                            </Link>

                            <Link
                                to="/register"
                                className="inline-flex items-center justify-center rounded-lg bg-amber-100 px-5 py-2.5 font-semibold text-stone-900 shadow hover:bg-amber-200"
                            >
                                Create account
                            </Link>
                        </div>
                    </div>
                </div>
            </div>

            {/* Content */}
            <div className="mx-auto max-w-6xl px-4 pb-14">
                <div className="grid gap-6 md:grid-cols-3">
                    {/* Parchment card */}
                    <div className="rounded-xl border border-stone-300 bg-amber-50 p-5 shadow">
                        <div className="text-xl font-bold text-stone-800">📜 Campaigns</div>
                        <p className="mt-2 text-sm text-stone-700">
                            DMs create campaigns, track sessions, and manage players.
                        </p>
                    </div>

                    <div className="rounded-xl border border-stone-300 bg-amber-50 p-5 shadow">
                        <div className="text-xl font-bold text-stone-800">🧙 Characters</div>
                        <p className="mt-2 text-sm text-stone-700">
                            Players keep a character per campaign — with stats and skills.
                        </p>
                    </div>

                    <div className="rounded-xl border border-stone-300 bg-amber-50 p-5 shadow">
                        <div className="text-xl font-bold text-stone-800">🛡️ Secure</div>
                        <p className="mt-2 text-sm text-stone-700">
                            JWT + refresh tokens with rotation, ready for Azure later.
                        </p>
                    </div>
                </div>

                {/* Footer-ish */}
                <div className="mt-10 rounded-xl border border-stone-300 bg-stone-50 p-5 shadow">
                    <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
                        <div>
                            <div className="font-semibold text-stone-800">Ready to begin?</div>
                            <div className="text-sm text-stone-600">
                                Log in to continue your adventures, or register to start fresh.
                            </div>
                        </div>

                        <div className="flex gap-4 justify-center">
                            <Button
                                variant="primary"
                                onClick={() => navigate("/register")}
                            >
                                Start Your Adventure
                            </Button>

                            <Button
                                variant="secondary"
                                onClick={() => navigate("/login")}
                            >
                                Continue Campaign
                            </Button>
                        </div>
                    </div>
                </div>
            </div>
        </div>
    );
}

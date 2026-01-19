import { useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { login } from "../api/authApi";
import { useAuth } from "../auth/AuthContext";
import Button from "../components/UI/Button";

function extractLoginError(err: any): string {
    const status = err?.response?.status;
    const data = err?.response?.data;

    // Typical auth failures
    if (status === 401) return "Invalid email or password.";

    // If backend returns a string message
    if (typeof data === "string") return data;

    // ASP.NET validation format
    if (data?.errors && typeof data.errors === "object") {
        const parts: string[] = [];
        for (const key of Object.keys(data.errors)) {
            const msgs = data.errors[key];
            if (Array.isArray(msgs)) parts.push(...msgs);
        }
        if (parts.length) return parts.join(" ");
    }

    return "Login failed. Please try again.";
}

export default function Login() {
    const [email, setEmail] = useState("");
    const [password, setPassword] = useState("");

    const [submitting, setSubmitting] = useState(false);
    const [error, setError] = useState<string | null>(null);

    const navigate = useNavigate();
    const auth = useAuth();

    const handleSubmit = async (e: React.FormEvent) => {
        e.preventDefault();
        setError(null);
        setSubmitting(true);

        try {
            const res = await login(email, password);

            const accessToken = res.data?.accessToken;
            if (!accessToken) throw new Error("No token returned");

            await auth.loginWithToken(accessToken);
            navigate("/dashboard");
        } catch (err: any) {
            setError(extractLoginError(err));
        } finally {
            setSubmitting(false);
        }
    };

    return (
        <div className="min-h-screen bg-stone-100 px-4 py-10">
            <div className="mx-auto max-w-5xl">
                <div className="grid gap-6 md:grid-cols-2">
                    {/* Left: flavor panel */}
                    <div className="rounded-xl border border-stone-300 bg-stone-900 text-amber-100 shadow overflow-hidden">
                        <div className="p-6">
                            <div className="inline-flex items-center gap-2 rounded-full bg-amber-100/10 px-3 py-1 text-sm font-semibold">
                                🎲 DnD Campaign Manager
                            </div>

                            <h1 className="mt-5 text-3xl font-extrabold tracking-tight">
                                Welcome back, adventurer.
                            </h1>

                            <p className="mt-3 text-amber-100/80">
                                Continue your campaign, manage characters, and keep your table organized.
                            </p>

                            <div className="mt-8 text-sm text-amber-100/70">
                                New here?{" "}
                                <Link
                                    to="/register"
                                    className="font-semibold text-amber-100 underline underline-offset-4 hover:text-white"
                                >
                                    Create an account
                                </Link>
                            </div>
                        </div>
                    </div>

                    {/* Right: form card */}
                    <div className="rounded-xl border border-stone-300 bg-amber-50 shadow p-6">
                        <div className="mb-6">
                            <h2 className="text-2xl font-bold text-stone-800">Login</h2>
                            <p className="text-sm text-stone-600">
                                Enter your credentials to continue.
                            </p>
                        </div>

                        {/* ✅ Inline error panel */}
                        {error && (
                            <div className="mb-4 rounded-lg border border-red-300 bg-red-50 px-4 py-3 text-sm text-red-800">
                                {error}
                            </div>
                        )}

                        <form onSubmit={handleSubmit} className="space-y-4">
                            <div>
                                <label className="block text-sm font-semibold text-stone-700">
                                    Email
                                </label>
                                <input
                                    value={email}
                                    onChange={e => setEmail(e.target.value)}
                                    placeholder="you@party.com"
                                    autoComplete="email"
                                    className="mt-1 w-full rounded-lg border border-stone-300 bg-white px-3 py-2 text-stone-900 shadow-sm focus:border-emerald-600 focus:outline-none focus:ring-2 focus:ring-emerald-200"
                                    required
                                />
                            </div>

                            <div>
                                <label className="block text-sm font-semibold text-stone-700">
                                    Password
                                </label>
                                <input
                                    type="password"
                                    value={password}
                                    onChange={e => setPassword(e.target.value)}
                                    placeholder="••••••••"
                                    autoComplete="current-password"
                                    className="mt-1 w-full rounded-lg border border-stone-300 bg-white px-3 py-2 text-stone-900 shadow-sm focus:border-emerald-600 focus:outline-none focus:ring-2 focus:ring-emerald-200"
                                    required
                                />
                            </div>

                            <Button
                                type="submit"
                                variant="primary"
                                disabled={submitting}
                                className="w-full"
                            >
                                {submitting ? "Logging in..." : "Login"}
                            </Button>

                            <div className="text-center text-sm text-stone-600">
                                Don’t have an account?{" "}
                                <Link
                                    to="/register"
                                    className="font-semibold text-stone-900 underline underline-offset-4 hover:text-stone-700"
                                >
                                    Register
                                </Link>
                            </div>

                            <div className="text-center">
                                <Button type="button" variant="ghost" onClick={() => navigate("/")}>
                                    ← Back to Landing
                                </Button>
                            </div>
                        </form>
                    </div>
                </div>

                <div className="h-10" />
            </div>
        </div>
    );
}

import { useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { register } from "../api/authApi";
import { useAuth } from "../auth/AuthContext";
import Button from "../components/UI/Button";

function extractRegisterError(err: any): string {
    const data = err?.response?.data.message;

    // Common: backend returns a plain string like "Email already registered"
    if (typeof data === "string") {
        if (data.toLowerCase().includes("Email already")) {
            return "That email is already registered. Try logging in instead.";
        }
        return data;
    }

    // Common: ASP.NET validation response
    // { title, status, errors: { Email: ["..."], Password: ["..."] } }
    if (data?.errors && typeof data.errors === "object") {
        const parts: string[] = [];
        for (const key of Object.keys(data.errors)) {
            const msgs = data.errors[key];
            if (Array.isArray(msgs)) parts.push(...msgs);
        }
        if (parts.length) return parts.join(" ");
    }

    // Fallback
    return "Registration failed. Please try again.";
}

export default function Register() {
    const [email, setEmail] = useState("");
    const [password, setPassword] = useState("");
    const [confirmPassword, setConfirmPassword] = useState("");

    const [submitting, setSubmitting] = useState(false);
    const [error, setError] = useState<string | null>(null);

    const navigate = useNavigate();
    const auth = useAuth();

    const handleSubmit = async (e: React.FormEvent) => {
        e.preventDefault();
        setError(null);

        // ✅ Confirm password check (frontend)
        if (password !== confirmPassword) {
            setError("Passwords do not match. Please confirm your password.");
            return;
        }

        setSubmitting(true);

        try {
            const res = await register(email, password);
            const accessToken = res.data?.accessToken;
            if (!accessToken) throw new Error("No token returned");

            await auth.loginWithToken(accessToken);
            navigate("/dashboard");
        } catch (err: any) {
            setError(extractRegisterError(err));
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
                                🧾 New Adventurer
                            </div>

                            <h1 className="mt-5 text-3xl font-extrabold tracking-tight">
                                Create your account.
                            </h1>

                            <p className="mt-3 text-amber-100/80">
                                Join a campaign, build characters, and start tracking your adventures.
                            </p>

                            <div className="mt-8 text-sm text-amber-100/70">
                                Already have an account?{" "}
                                <Link
                                    to="/login"
                                    className="font-semibold text-amber-100 underline underline-offset-4 hover:text-white"
                                >
                                    Login
                                </Link>
                            </div>
                        </div>
                    </div>

                    {/* Right: form card */}
                    <div className="rounded-xl border border-stone-300 bg-amber-50 shadow p-6">
                        <div className="mb-6">
                            <h2 className="text-2xl font-bold text-stone-800">Register</h2>
                            <p className="text-sm text-stone-600">
                                Create your account to enter the tavern.
                            </p>
                        </div>

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
                                    placeholder="At least 8 characters"
                                    autoComplete="new-password"
                                    className="mt-1 w-full rounded-lg border border-stone-300 bg-white px-3 py-2 text-stone-900 shadow-sm focus:border-emerald-600 focus:outline-none focus:ring-2 focus:ring-emerald-200"
                                    required
                                    minLength={8}
                                />
                                <p className="mt-1 text-xs text-stone-600">
                                    Tip: Use 8+ characters (tighten rules later).
                                </p>
                            </div>

                            <div>
                                <label className="block text-sm font-semibold text-stone-700">
                                    Confirm Password
                                </label>
                                <input
                                    type="password"
                                    value={confirmPassword}
                                    onChange={e => setConfirmPassword(e.target.value)}
                                    placeholder="Repeat password"
                                    autoComplete="new-password"
                                    className="mt-1 w-full rounded-lg border border-stone-300 bg-white px-3 py-2 text-stone-900 shadow-sm focus:border-emerald-600 focus:outline-none focus:ring-2 focus:ring-emerald-200"
                                    required
                                    minLength={8}
                                />
                            </div>

                            <Button
                                type="submit"
                                variant="primary"
                                disabled={submitting}
                                className="w-full"
                            >
                                {submitting ? "Creating account..." : "Create account"}
                            </Button>

                            <div className="text-center text-sm text-stone-600">
                                Already registered?{" "}
                                <Link
                                    to="/login"
                                    className="font-semibold text-stone-900 underline underline-offset-4 hover:text-stone-700"
                                >
                                    Login
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

import { Link, useLocation, useNavigate } from "react-router-dom";
import { useAuth } from "../auth/AuthContext";
import { useMemo, useState } from "react";

function NavLink({
    to,
    label,
    onClick
}: {
    to: string;
    label: string;
    onClick?: () => void;
}) {
    const location = useLocation();
    const isActive = useMemo(() => location.pathname === to, [location.pathname, to]);

    return (
        <Link
            to={to}
            onClick={onClick}
            className={[
                "px-3 py-2 rounded-lg text-sm font-semibold transition",
                isActive
                    ? "bg-stone-900 text-amber-100 shadow"
                    : "text-stone-700 hover:bg-stone-100 hover:text-stone-900"
            ].join(" ")}
        >
            {label}
        </Link>
    );
}

export default function Navbar() {
    const auth = useAuth();
    const navigate = useNavigate();
    const [open, setOpen] = useState(false);

    const handleLogout = async () => {
        setOpen(false);
        await auth.logout();
        navigate("/login");
    };

    const close = () => setOpen(false);

    return (
        <header className="sticky top-0 z-50 border-b border-stone-300 bg-amber-50/90 backdrop-blur">
            <div className="mx-auto max-w-6xl px-4">
                <div className="flex h-14 items-center justify-between">
                    {/* Brand */}
                    <Link
                        to="/"
                        className="flex items-center gap-2 rounded-lg px-2 py-1 hover:bg-stone-100"
                        onClick={close}
                    >
                        <span className="text-xl">🎲</span>
                        <span className="font-extrabold tracking-tight text-stone-900">
                            DnD Campaign Manager
                        </span>
                    </Link>

                    {/* Desktop nav */}
                    <nav className="hidden md:flex items-center gap-2">
                        {!auth.token ? (
                            <>
                                <NavLink to="/login" label="Login" />
                                <NavLink to="/register" label="Register" />
                            </>
                        ) : (
                            <>
                                <NavLink to="/dashboard" label="Dashboard" />
                                <button
                                    onClick={handleLogout}
                                    className="ml-2 rounded-lg bg-red-800 px-4 py-2 text-sm font-semibold text-white shadow hover:bg-red-700"
                                >
                                    Logout
                                </button>
                            </>
                        )}
                    </nav>

                    {/* Mobile button */}
                    <button
                        className="md:hidden inline-flex items-center justify-center rounded-lg border border-stone-300 bg-white px-3 py-2 text-sm font-semibold text-stone-800 hover:bg-stone-100"
                        onClick={() => setOpen(v => !v)}
                        aria-label="Toggle navigation menu"
                    >
                        {open ? "✕" : "☰"}
                    </button>
                </div>

                {/* Mobile dropdown */}
                {open && (
                    <div className="md:hidden pb-4">
                        <div className="mt-2 rounded-xl border border-stone-300 bg-stone-50 p-3 shadow">
                            {!auth.token ? (
                                <div className="flex flex-col gap-2">
                                    <NavLink to="/login" label="Login" onClick={close} />
                                    <NavLink to="/register" label="Register" onClick={close} />
                                </div>
                            ) : (
                                <div className="flex flex-col gap-2">
                                    <NavLink to="/dashboard" label="Dashboard" onClick={close} />
                                    <button
                                        onClick={handleLogout}
                                        className="rounded-lg bg-red-800 px-4 py-2 text-sm font-semibold text-white shadow hover:bg-red-700"
                                    >
                                        Logout
                                    </button>
                                </div>
                            )}

                            {/* Optional: show who is logged in */}
                            {auth.user?.email && (
                                <div className="mt-3 text-xs text-stone-600">
                                    Signed in as <span className="font-semibold">{auth.user.email}</span>
                                </div>
                            )}
                        </div>
                    </div>
                )}
            </div>
        </header>
    );
}

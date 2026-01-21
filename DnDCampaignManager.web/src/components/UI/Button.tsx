import type { ButtonHTMLAttributes, ReactNode } from "react";

type Variant = "primary" | "secondary" | "danger" | "ghost";
type Size = "sm" | "md";

type Props = ButtonHTMLAttributes<HTMLButtonElement> & {
    variant?: Variant;
    size?: Size;
    children: ReactNode;
};

export default function Button({
    variant = "primary",
    size = "md",
    children,
    className = "",
    ...props
}: Props) {
    const base =
        "inline-flex items-center justify-center font-semibold rounded-lg transition shadow-sm focus:outline-none focus:ring-2 focus:ring-offset-2";

    const variants: Record<Variant, string> = {
        primary:
            "bg-emerald-700 text-white hover:bg-emerald-600 focus:ring-emerald-700",
        secondary:
            "bg-stone-200 text-stone-800 hover:bg-stone-300 focus:ring-stone-400",
        danger:
            "bg-red-800 text-white hover:bg-red-700 focus:ring-red-800",
        ghost:
            "bg-transparent text-stone-700 hover:bg-stone-100 focus:ring-stone-300"
    };

    const sizes: Record<Size, string> = {
        sm: "px-3 py-1.5 text-sm",
        md: "px-4 py-2 text-sm"
    };

    return (
        <button
            {...props}
            className={[
                base,
                variants[variant],
                sizes[size],
                className
            ].join(" ")}
        >
            {children}
        </button>
    );
}

type Props = {
    message?: string | null;
    className?: string;
};

export default function ErrorPanel({ message, className = "" }: Props) {
    if (!message) return null;

    return (
        <div
            className={[
                "rounded-lg border border-red-300 bg-red-50 px-4 py-3 text-sm text-red-800",
                className
            ].join(" ")}
            role="alert"
            aria-live="polite"
        >
            {message}
        </div>
    );
}

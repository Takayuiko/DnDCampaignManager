import { useAuth } from "../auth/AuthContext";

export default function DevTools() {
    const auth = useAuth();

    return (
        <div>
            <h2>Dev Tools</h2>

            <button onClick={() => localStorage.removeItem("token")}>
                Clear Access Token
            </button>

            <button onClick={() => auth.logout()}>
                Full Logout
            </button>

            <pre>
                {JSON.stringify(auth, null, 2)}
            </pre>
        </div>
    );
}

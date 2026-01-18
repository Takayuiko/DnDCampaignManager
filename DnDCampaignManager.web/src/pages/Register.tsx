import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { register } from "../api/authApi";
import { useAuth } from "../auth/AuthContext";

export default function Register() {
    const [email, setEmail] = useState("");
    const [password, setPassword] = useState("");
    const navigate = useNavigate();
    const auth = useAuth();

    const handleSubmit = async (e: React.FormEvent) => {
        e.preventDefault();

        try {
            const res = await register(email, password);
            await auth.loginWithToken(res.data.accessToken);
            navigate("/dashboard");
        } catch (err: any) {
            auth.resetAuth();
            alert(err?.response?.data ?? "Registration failed");
        }
    };

    return (
        <form onSubmit={handleSubmit}>
            <h2>Register</h2>

            <input
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                placeholder="Email"
            />

            <input
                type="password"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                placeholder="Password"
            />

            <button type="submit">Register</button>
        </form>
    );
}

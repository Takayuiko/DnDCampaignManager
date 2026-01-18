import { Link } from "react-router-dom";

export default function Landing() {
    return (
        <div>
            <h1>Welcome</h1>
            <Link to="/login">Login</Link>
            <Link to="/register">Register</Link>
        </div>
    );
}

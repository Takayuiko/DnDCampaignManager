import { Link, useNavigate } from "react-router-dom";
import { useAuth } from "../auth/AuthContext";

export default function Navbar() {
    const { user, logout } = useAuth();
    const navigate = useNavigate();

    const handleLogout = async () => {
        await logout();
        navigate("/login");
    };

    return (
        <nav style={styles.nav}>
            <Link to="/" style={styles.link}>
                DnD Campaign Manager
            </Link>

            <div>
                {!user ? (
                    <>
                        <Link to="/login" style={styles.link}>
                            Login
                        </Link>
                        <Link to="/register" style={styles.link}>
                            Register
                        </Link>
                    </>
                ) : (
                    <>
                        <Link to="/dashboard" style={styles.link}>
                            Dashboard
                        </Link>
                        <button onClick={handleLogout} style={styles.button}>
                            Logout
                        </button>
                    </>
                )}
            </div>
        </nav>
    );
}



const styles = {
    nav: {
        display: "flex",
        justifyContent: "space-between",
        padding: "1rem",
        borderBottom: "1px solid #ccc",
    },
    link: {
        marginRight: "1rem",
        textDecoration: "none",
    },
    button: {
        background: "none",
        border: "none",
        cursor: "pointer",
        color: "blue",
    },
};

"use client";

import Link from "next/link";
import {useEffect, useState} from "react";
import {LoginModal} from "./LoginModal";
import {RegisterModal} from "./RegisterModal";

export default function Navbar({ onAuthStatusChange }: { onAuthStatusChange?: () => void }) {
    const [token, setToken] = useState<string | null>(null);
    const [username, setUsername] = useState<string | null>(null);
    const [showLoginModal, setShowLoginModal] = useState(false);
    const [showRegisterModal, setShowRegisterModal] = useState(false);

    useEffect(() => {
        setToken(localStorage.getItem("token"));
        setUsername(localStorage.getItem("username"));
    }, []);

    const handleLogout = () => {
        localStorage.removeItem("token");
        localStorage.removeItem("username");
        setToken(null);
        setUsername(null);
        // Notify parent component about auth status change
        if (onAuthStatusChange) {
            onAuthStatusChange();
        }
    }

    const handleLoginSuccess = () => {
        setToken(localStorage.getItem("token"));
        setUsername(localStorage.getItem("username"));
        // Notify parent component about auth status change
        if (onAuthStatusChange) {
            onAuthStatusChange();
        }
    }

    const handleRegisterSuccess = () => {
        setToken(localStorage.getItem("token"));
        setUsername(localStorage.getItem("username"));
        // Notify parent component about auth status change
        if (onAuthStatusChange) {
            onAuthStatusChange();
        }
    }

    return (
        <>
            <div className="flex justify-between items-center p-4 shadow">
                <h1 className="text-xl font-bold">🎬 MovieApp</h1>
                <div>
                    {!token ? (
                        <>
                            <button
                                onClick={() => setShowLoginModal(true)}
                                className="mr-4"
                                >
                                    Login
                                </button>                        
                                <button
                                    onClick={() => setShowRegisterModal(true)}
                                    className="bg-green-500 text-white px-4 py-2 rounded"
                                >
                                    Register
                                </button>
                        </>
                    ): (                        
                        <div className="flex items-center gap-4">
                            <span className="text-green-600">Welcome, {username}</span>
                            <button
                                onClick={handleLogout}
                                className="bg-red-500 text-white px-4 py-2 rounded"
                            >
                                Logout
                            </button>
                        </div>
                    )}
                </div>
            </div>

            <LoginModal
                isOpen={showLoginModal}
                onClose={() => setShowLoginModal(false)}
                onLoginSuccess={handleLoginSuccess}
            />

            <RegisterModal
                isOpen={showRegisterModal}
                onClose={() => setShowRegisterModal(false)}
                onRegisterSuccess={handleRegisterSuccess}
            />
        </>
    );
}
"use client";
import {useState} from "react";
import {register} from "@/lib/api";
import { useRouter } from "next/navigation";


interface RegisterModalProps {
    isOpen: boolean;
    onClose: () => void;
    onRegisterSuccess: () => void;
}

export function RegisterModal({isOpen, onClose, onRegisterSuccess}: RegisterModalProps){
    const [username, setUsername] = useState("");
    const [password, setPassword] = useState("");
    const [confirmPassword, setConfirmPassword] = useState("");
    const [error, setError] = useState("");
    const router = useRouter();
    
    const handleRegister = async () => {
        // Validation
        if (!username.trim()) {
            setError("Username is required");
            return;
        }
        
        if (!password) {
            setError("Password is required");
            return;
        }
        
        if (password !== confirmPassword) {
            setError("Passwords do not match");
            return;
        }
        
        if (password.length < 6) {
            setError("Password must be at least 6 characters");
            return;
        }

        try {
            const data = await register(username, password);
            localStorage.setItem("token", data.accessToken);
            localStorage.setItem("username", username);
            onRegisterSuccess();
            onClose();
            router.push("/");
        } catch (error : any) {
            setError(error.message || "Registration failed");
        }
    };

    if(!isOpen) return null;

    return (
        <div className="fixed inset-0 z-50 flex items-center justify-center">
            <div
                className="fixed inset-0 bg-black/30 backdrop-blur-sm"
                onClick={onClose}
                />
                <div className="bg-white rounded-lg p-8 relative z-10 max-w-md w-full mx-4 border-2 border-gray-300 shadow-2xl">
                    <button
                        onClick={onClose}
                        className="absolute top-4 right-4 text-gray-500 hover:text-gray-700 text-2xl"
                        >
                        &times;
                    </button>

                    <h1 className="text-xl font-bold mb-4">Register</h1>
                    {error && (
                        <div className="bg-red-100 border border-red-400 text-red-700 px-4 py-3 rounded mb-4">
                            {error}
                        </div>
                    )}

                    <input
                        className="border p-2 w-full mb-3"
                        placeholder="Username"
                        value={username}
                        onChange={(e) => {
                            setUsername(e.target.value);
                            setError("");}}
                    />

                    <input
                        type="password"
                        className="border p-2 w-full mb-3"
                        placeholder="Password"
                        value={password}
                        onChange={(e) => {
                            setPassword(e.target.value);
                            setError("");
                        }}
                    />

                    <input
                        type="password"
                        className="border p-2 w-full mb-4"
                        placeholder="Confirm Password"
                        value={confirmPassword}
                        onChange={(e) => {
                            setConfirmPassword(e.target.value);
                            setError("");
                        }}
                    />

                    <button
                        onClick={handleRegister}
                        className="bg-green-500 text-white px-4 py-2 w-full hover:bg-green-600"
                        >
                            Register                            
                        </button>
                </div>
        </div>
    );
}

"use client";
import { useState } from "react";
import { useRouter } from "next/navigation";
import { login } from "@/lib/api";

export default function Login() {
    const [username, setUsername] = useState("");
    const [password, setPassword] = useState("");

    const handleLogin = async () => {
        const data = await login(username, password);
        localStorage.setItem("accessToken", data.accessToken);
        alert("Login successful!");
        }

    return(
        <div className="p-8 max-w-md mx-auto">
            <h1 className="text-xl font-bold mb-4">Login</h1>
            <input
                className="border p-2 w-full mb-3"
                placeholder="Username"
                onChange={(e) => setUsername(e.target.value)}
            />
            <input
                type="password"
                className="border p-2 w-full mb-3"
                placeholder="Password"
                onChange={(e)=> setPassword(e.target.value)}
            />
            <button
                onClick={handleLogin}
                className="bg-blue-500 text-white px-4 py-2 w-full"
                >
                Login
                </button>
        </div>
    );
}

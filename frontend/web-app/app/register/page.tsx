"use client";

import {useState}  from "react";
import { register } from "@/lib/api";

export default function Register() {
    const [usename, SetUsername] = useState("");
    const [password, SetPassword] = useState("");

    const handleRegister = async () => {
        await register(usename, password);
        alert("Registration successful! Please login.");
    }

    return (
        <div className="p-8 max-w-md mx-auto">
            <h1 className="text-xl font-bold mb-4">Register</h1>
            <input
                className="border p-2 w-full mb-3"
                placeholder="Username"
                onChange={(e) => SetUsername(e.target.value)}
            />
            <input
                type="password"
                className="border p-2 w-full mb-3"
                placeholder="Password"
                onChange={(e)=>SetPassword(e.target.value)}
            />
            <button
                onClick={handleRegister}
                className="bg-green-500 text-white px-4 py-2 w-full"
                >Register
                </button>
        </div>
    );
}
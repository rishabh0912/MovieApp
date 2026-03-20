import { Console } from "console";

const IDENTITY_BASE = process.env.NEXT_PUBLIC_IDENTITY_URL || "http://localhost:5001"
const MOVIE_BASE = process.env.NEXT_PUBLIC_MOVIE_URL || "http://localhost:5002"
const RATING_BASE = process.env.NEXT_PUBLIC_RATING_URL || "http://localhost:5003"

export async function getMovies(page: number=1, pageSize: number=10) {
    const res = await fetch(`${MOVIE_BASE}/movies?page=${page}&pageSize=${pageSize}`, {cache: 'no-store'});
    return res.json();   
}

export async function login(username:string, password: string){
    const rest = await fetch(`${IDENTITY_BASE}/auth/login`, {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json'
        },
        body: JSON.stringify({username, password})
    });
    if (!rest.ok) {
        const error = await rest.json();
        console.log("Login error:", error);
        throw new Error(error.message || "Login failed");
    }
    return rest.json();
}

export async function register(username:string, password: string){
    const res = await fetch(`${IDENTITY_BASE}/auth/register`, {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json'
        },
        body: JSON.stringify({username, password})
    });
    return res.json();
}

export async function getMovieById(id: string) {
    const res = await fetch(`${MOVIE_BASE}/movies/${id}`, {cache: 'no-store'});
    return res.json();
}

export async function getMyRatedMovies(page: number=1, pageSize: number=10)
{
    const token = localStorage.getItem("token");

    if (!token) {
        throw new Error("User not authenticated");
    }

    const res = await fetch(
        `${RATING_BASE}/rating/user/movies?page=${page}&pageSize=${pageSize}`,
        {
            headers: {
                'Authorization': `Bearer ${token}`                
            },
            cache: 'no-store'
        }
    );

    if (!res.ok) {
        throw new Error("Failed to fetch rated movies");
    }

    return res.json();
}

export async function rateMovie(movieId: string, score: number, review: string = "") {
    const token = localStorage.getItem("token");

    const res = await fetch(`${RATING_BASE}/rating`, {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
            'Authorization': `Bearer ${token}`
        },
        body: JSON.stringify({movieId, score, review})
    });
    
    if (!res.ok) {
        const error = await res.json();
        throw new Error(error.message || "Failed to submit rating");
    }

    return res.json();
}



import MovieCard from "./MovieCard";

interface MovieGridProps {
    movies: any[];
    onMovieClick: (movie: any) => void;
}

export default function MovieGrid({movies, onMovieClick}: MovieGridProps){
    return (
        <div className="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-4 gap-4 p-4 w-full">
            {movies.map((movie) => (
                <MovieCard
                    key={movie.id}
                    movie={movie}
                    onClick={() => onMovieClick(movie)}
                />
            ))}
        </div>        
    );
}

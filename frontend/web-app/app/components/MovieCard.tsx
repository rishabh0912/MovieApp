interface MovieCardProps {
    movie: any;
    onClick: () => void;
}

export default function MovieCard({movie, onClick}: MovieCardProps) {
    return (
        <div 
            onClick={onClick}
            className="cursor-pointer hover:scale-105 transition-transform duration-200">
            <img 
                src={movie?.posterUrl}
                alt={movie?.title}
                className="w-full h-auto rounded-lg shadow-md hover:shadow-xl"
            />
            <h3 className="mt-2 font-semibold text-sm">{movie.title}</h3>

            {/* Show user rating if it exists */}
            {movie.userRating && (
                <div className="flex items-center gap-1 text-sm">
                    <span className="text-blue-500">⭐</span>
                    <span className="text-blue-700 font-semibold">You: {movie.userRating}/10</span>
                </div>
            )}

            {/* Show average rating */}
            {movie.averageRating && (
                <p className="text-yellow-500 text-sm">
                    ⭐ {movie.averageRating.toFixed(1)}/10
                </p>
            )}
        </div>
    );
}
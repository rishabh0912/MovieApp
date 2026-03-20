"use client";
import { rateMovie } from '@/lib/api';
import {useState, useEffect} from 'react';

interface MovieDetailModalProps {
    movie: any | null;
    onClose: () => void;
    onRatingSubmitted?: () => void;
    isLoggedIn?: boolean;
}

export function MovieDetailModal({movie, onClose, onRatingSubmitted, isLoggedIn=false}: MovieDetailModalProps)
{
    const [selectedRating, setSelectedRating] = useState<number>(0);
    const [submitting, setSubmitting] = useState(false);
    const [error, setError] = useState("");
    const [review, setReview] = useState("");
    
    useEffect(() => {
        if (movie?.userRating) {
            setSelectedRating(movie.userRating);
        } else if (movie?.score) {
            // Handle if API returns 'score' instead of 'userRating'
            setSelectedRating(movie.score);
        } else {
            setSelectedRating(0);
        }
        setReview("");
        setError("");
    }, [movie]);

    if (!movie) return null;

    const handleSubmitRating = async () => {
        if (!isLoggedIn) {
            setError("Please login first to submit a rating.");
            return;
        }

        if (selectedRating === 0) {
            setError("Please select a rating before submitting.");
            return;
        }

        setSubmitting(true);
        setError("");
        
        try {
            await rateMovie(movie.id, selectedRating, review);
            alert("Rating submitted successfully!");
            if (onRatingSubmitted) {
                onRatingSubmitted();
            }
            onClose();
        } catch (error : any) {
            setError(error.message || "Failed to submit rating. Please try again.");
        } finally {
            setSubmitting(false);
        }
    };

    return (
        <div className="fixed inset-0 z-50 flex items-center justify-center">
            {/* Backdrop */}
            <div
                className="fixed inset-0 bg-black/50 backdrop-blur-sm"
                onClick={onClose}
            />

            {/* Modal */}
            <div className="bg-white rounded-lg p-8 relative z-10 max-w-4xl w-full mx-4 max-h-[90vh] overflow-y-auto">
                {/* Close Button */}
                <button
                    onClick={onClose}
                    className="absolute top-4 right-4 text-gray-500 hover:text-gray-700 text-3xl"
                >
                    &times;
                </button>

                <div className="flex flex-col md:flex-row gap-6">
                    {/* Left: Movie Poster */}
                    <div className="md:w-1/3">
                        <img
                            src={movie.posterUrl || movie.poster}
                            alt={movie.title}
                            className="w-full rounded-lg shadow-lg"                            
                        />                        
                    </div>

                    {/* Right: Movie Details */}
                    <div className="md:w-2/3">
                        <h2 className="text-3xl font-bold mb-2">{movie.title}</h2>
                        <p className="text-gray-600 mb-4">
                            {movie.releaseYear} • {movie.genre} 
                        </p>

                        {/* Movie Description */}
                        <div className="mb-6">
                            <h3 className="text-xl font-semibold mb-2">Description</h3>
                            <p className="text-gray-700">{movie.description || 'No description available'}</p>
                        </div>

                        {/* Cast */}
                        {movie.cast && (
                            <div className="mb-6">
                                <h3 className="text-xl font-semibold mb-2">Cast</h3>
                                <p className="text-gray-700">{movie.cast}</p>
                            </div>
                        )}

                        {/* Ratings Section */}
                        <div className="mb-6 bg-gray-50 p-4 rounded-lg">
                            <h3 className="text-xl font-semibold mb-3">Ratings</h3>

                            {/* Overall Rating */}
                            <div className="flex items-center gap-4 mb-3">
                                <span className="text-yellow-500 text-xl">⭐</span>
                                <span className="font-semibold">Overall:</span>
                                <span className="text-lg">
                                    {movie.averageRating?.toFixed(1) || 'N/A'}/10
                                </span>
                                {movie.ratingCount > 0 && (
                                    <span className="text-gray-500">
                                        ({movie.ratingCount.toLocaleString()} ratings)
                                    </span>
                                )}
                            </div>

                            {/* User's Rating */}
                            {(movie.userRating && movie.userRating > 0) || (movie.score && movie.score > 0) ? (
                                <div className="flex items-center gap-4 bg-blue-50 p-3 rounded border border-blue-200">
                                    <span className="text-blue-500 text-xl">⭐</span>
                                    <span className="font-semibold text-blue-700">Your Rating:</span>
                                    <span className="text-lg text-blue-700 font-bold">
                                        {(movie.userRating || movie.score)}/10
                                    </span>
                                </div>
                            ) : null}
                        </div>

                        {/* Rate this movie */}
                        <div className="border-t pt-4">
                            <h3 className="text-lg font-semibold mb-3">
                                {(movie.userRating > 0 || movie.score > 0) ? "Update Your Rating" : "Rate This Movie"}
                            </h3>

                            {/* Error Message */}
                            {error && (
                                <div className="bg-red-100 border border-red-400 text-red-700 px-4 py-2 rounded mb-3">
                                    {error}
                                </div>
                            )}

                            {/* Star Rating */}
                            <div className="flex gap-1 mb-4">
                                {[1,2,3,4,5,6,7,8,9,10].map((rating) => (
                                    <button
                                        key={rating}
                                        onClick={() => {
                                            setSelectedRating(rating);
                                            setError("");
                                        }}
                                        disabled={submitting}
                                        className={`text-2xl ${
                                            selectedRating >= rating 
                                            ? "text-yellow-500"
                                            : "text-gray-300"
                                        } hover:text-yellow-400 transition-colors`}
                                    >
                                        ★
                                    </button>
                                ))}
                            </div>

                            {/* Review Text Area */}
                            <div className="mb-4">
                                <label className="block text-sm font-medium text-gray-700 mb-2">
                                    Review (Optional)
                                </label>
                                <textarea
                                    value={review}
                                    onChange={(e) => setReview(e.target.value)}
                                    placeholder="Share your thoughts about this movie..."
                                    className="w-full border border-gray-300 rounded-lg p-3 focus:ring-2 focus:ring-blue-500 focus:border-transparent"
                                    rows={4}
                                    disabled={submitting}
                                />
                            </div>

                            {/* Submit Button */}
                            <div className="flex items-center gap-3">
                                <button
                                    onClick={handleSubmitRating}
                                    disabled={selectedRating === 0 || submitting}
                                    className="bg-blue-500 text-white px-6 py-2 rounded disabled:bg-gray-300 hover:bg-blue-600 transition-colors"
                                >
                                    {submitting 
                                        ? "Submitting..." 
                                        : (movie.userRating > 0 || movie.score > 0)
                                            ? "Update Rating" 
                                            : "Submit Rating"
                                    }
                                </button>
                                {selectedRating > 0 && (
                                    <span className="text-gray-600">
                                        Selected: {selectedRating}/10
                                    </span>
                                )}
                            </div>
                        </div>
                    </div> 
                </div>
            </div>
        </div>
    );
}

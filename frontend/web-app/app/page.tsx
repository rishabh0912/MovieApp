"use client";

import Navbar from "./components/Navbar";
import MovieGrid from "./components/MovieGrid";
import { getMovies, getMyRatedMovies } from "@/lib/api";
import { MovieDetailModal } from "./components/MovieDetailModal";
import {useState, useEffect} from "react";

export default function Home() {
  const [allMovies, setAllMovies] = useState<any[]>([]);
  const [myRatedMovies, setMyRatedMovies] = useState<any[]>([]);
  const [selectedMovie, setSelectedMovie] = useState<any>(null);
  const [loading, setLoading] = useState(false);
  const [isLoggedIn, setIsLoggedIn] = useState(false);
  
  // Pagination state
  const [allMoviesPage, setAllMoviesPage] = useState(1);
  const [allMoviesTotalPages, setAllMoviesTotalPages] = useState(0);
  const [myRatedMoviesPage, setMyRatedMoviesPage] = useState(1);
  const [myRatedMoviesTotalPages, setMyRatedMoviesTotalPages] = useState(0);
  const pageSize = 10;

  useEffect(() => {
    checkLoginStatus();
    fetchAllMovies(1);
  }, []);    

  const checkLoginStatus = () => {
    const token = localStorage.getItem("token");
    setIsLoggedIn(!!token);

    if (token) {
      fetchMyRatedMovies(1);
    } else {
      setMyRatedMovies([]);
      setMyRatedMoviesPage(1);
    }
  };

  const fetchAllMovies = async (page: number) => {
    setLoading(true);
    try {
      const response = await getMovies(page, pageSize);
      
      // API returns a flat array (no pagination metadata)
      let movies = Array.isArray(response) ? response : (response.data || response.items || []);
      
      // Logic: if we get fewer items than pageSize, we're on the last page
      // if we get exactly pageSize items, there might be more pages
      const hasMore = movies.length === pageSize;
      const totalPages = hasMore ? page + 1 : page;
      
      setAllMovies(movies);
      setAllMoviesPage(page);
      setAllMoviesTotalPages(totalPages);
      
      console.log(`Page ${page}: got ${movies.length} movies, hasMore: ${hasMore}, totalPages: ${totalPages}`);
    } catch (error) {
      console.error("Error fetching movies:", error);
    } finally {
      setLoading(false);
    }
  };

  const fetchMyRatedMovies = async (page: number) => {
    try {
      const response = await getMyRatedMovies(page, pageSize);
      console.log("My Rated Movies Response:", response);
      
      // Handle different response formats
      let movies = Array.isArray(response) ? response : (response.data || []);
      let totalPages = response.pagination?.totalPages || Math.ceil(movies.length / pageSize) || 1;
      
      setMyRatedMovies(movies);
      setMyRatedMoviesPage(page);
      setMyRatedMoviesTotalPages(totalPages);
      console.log("Set myRatedMovies to:", movies);
    } catch (error) {
      console.error("Error fetching my rated movies:", error);
    }
  };

  const handleMovieClick = (movie: any) => {
    setSelectedMovie(movie);
  };

  const handleCloseModal = () => {
    setSelectedMovie(null);
  };

  const handleRatingSubmitted = () => {
    // Refresh the current page of both grids
    fetchAllMovies(allMoviesPage);
    if (isLoggedIn) {
      fetchMyRatedMovies(myRatedMoviesPage);
    }
  };

  const handleAuthStatusChange = () => {
    // Called when user logs in/out via Navbar
    checkLoginStatus();
    fetchAllMovies(1); // Reset to page 1
  };

  return (
    <div>
      <Navbar onAuthStatusChange={handleAuthStatusChange} />
      {loading ? (
        <p className="text-center py-8">Loading movies....</p>
      ): (
        <>
        {/* My Rated Movies Section - Only show if logged in */}
        {isLoggedIn && myRatedMovies.length > 0 && (
          <div className="mb-8 bg-blue-50 py-4">
            <div className="flex justify-between items-center px-4 py-2">
              <h2 className="text-2xl font-bold text-blue-700">🎬 My Rated Movies</h2>
              <span className="text-gray-600">{myRatedMovies.length} movies</span>
            </div>
            <MovieGrid
              movies={myRatedMovies.map(item => {
                // Handle different response formats
                if (item.movie) {
                  // Format: {movie: {...}, userRating: ...}
                  return {
                    ...item.movie,
                    userRating: item.userRating || item.score // Use score if userRating not present
                  };
                }
                // Format: direct movie object with userRating/score already included
                return item;
              })}
              onMovieClick={handleMovieClick}
            />
            {/* Pagination Controls */}
            <div className="flex justify-center items-center gap-4 mt-4 pb-4">
              <button
                onClick={() => fetchMyRatedMovies(myRatedMoviesPage - 1)}
                disabled={myRatedMoviesPage === 1}
                className="px-4 py-2 bg-blue-500 text-white rounded disabled:bg-gray-300"
              >
                Previous
              </button>
              <span className="text-gray-700">
                Page {myRatedMoviesPage} of {myRatedMoviesTotalPages}
              </span>
              <button
                onClick={() => fetchMyRatedMovies(myRatedMoviesPage + 1)}
                disabled={myRatedMoviesPage >= myRatedMoviesTotalPages}
                className="px-4 py-2 bg-blue-500 text-white rounded disabled:bg-gray-300"
              >
                Next
              </button>
            </div>
          </div>
        )}

        {/* All Movies Section */}
        <div>
          <h2 className="text-2xl font-bold p-4">All Movies</h2>
          <MovieGrid
            movies={allMovies}
            onMovieClick={handleMovieClick}
          />
          {/* Pagination Controls */}
          <div className="flex justify-center items-center gap-4 mt-4 pb-4">
            <button
              onClick={() => fetchAllMovies(allMoviesPage - 1)}
              disabled={allMoviesPage === 1}
              className="px-4 py-2 bg-blue-500 text-white rounded disabled:bg-gray-300"
            >
              Previous
            </button>
            <span className="text-gray-700">
              Page {allMoviesPage} of {allMoviesTotalPages}
            </span>
            <button
              onClick={() => fetchAllMovies(allMoviesPage + 1)}
              disabled={allMoviesPage >= allMoviesTotalPages}
              className="px-4 py-2 bg-blue-500 text-white rounded disabled:bg-gray-300"
            >
              Next
            </button>
          </div>
        </div>
        </>
      )}
      <MovieDetailModal
        movie={selectedMovie}
        onClose={handleCloseModal}
        onRatingSubmitted={handleRatingSubmitted}
        isLoggedIn={isLoggedIn}
      />
    </div>
  );
}
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using MovieService.Application.Interfaces;
using MovieService.Domain.Entities;
using MovieService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace MovieService.Infrastructure.Repository
{
    public class MovieRepository : IMovieRepository
    {
        private readonly AppDbContext _context;

        public MovieRepository(AppDbContext context)
        {
            _context = context;
        }
        public async Task<List<Genre>> GetGenres()
        {
            return await _context.Genres
                .ToListAsync();
        }

        public async Task<Movie?> GetMovieById(Guid id)
        {
            return await _context.Movies
                .Include(m => m.MovieGenres)
                    .ThenInclude(mg => mg.Genre)
                .Include(m => m.MovieCasts)
                    .ThenInclude(mc => mc.Cast)
                .FirstOrDefaultAsync(m => m.Id == id);
        }

        public async Task<IEnumerable<Movie>> GetMovies(int page, int pageSize)
        {
            return await _context.Movies
                .Include(m => m.MovieGenres)
                    .ThenInclude(mg => mg.Genre)
                .Include(m => m.MovieCasts)
                    .ThenInclude(mc => mc.Cast)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<IEnumerable<Movie>> GetMoviesByGenre(int genreId)
        {
            return await _context.Movies
                .Include(m => m.MovieGenres)
                    .ThenInclude(mg => mg.Genre)
                .Include(m => m.MovieCasts)
                    .ThenInclude(mc => mc.Cast)
                .Where(m => m.MovieGenres.Any(mg => mg.GenreId == genreId))
                .ToListAsync();
        }

        public async Task<IEnumerable<Movie>> GetMoviesByIds(IEnumerable<Guid> movieIds)
        {
            return await _context.Movies
                .Include(m => m.MovieGenres)
                    .ThenInclude(mg => mg.Genre)
                .Include(m => m.MovieCasts)
                    .ThenInclude(mc => mc.Cast)
                .Where(m => movieIds.Contains(m.Id))
                .ToListAsync();
        }

        public async Task UpdatedAverageRating(Guid movieId, int newScore, int oldScore, string eventType)
        {
            var movie = await _context.Movies.FindAsync(movieId);
            if (movie == null)
            {
                return;
            }

            if (eventType == "RatingAdded")
            {
                movie.AverageRating = ((movie.AverageRating * movie.RatingCount) + newScore) / (movie.RatingCount + 1);
                movie.RatingCount += 1;
            }
            else if (eventType == "RatingUpdated")
            {
                movie.AverageRating = ((movie.AverageRating * movie.RatingCount) - oldScore + newScore) / movie.RatingCount;
            }
            else if (eventType == "RatingDeleted")
            {
                if (movie.RatingCount > 1)
                {
                    movie.AverageRating = ((movie.AverageRating * movie.RatingCount) - oldScore) / (movie.RatingCount - 1);
                }
                else
                {
                    movie.AverageRating = 0;
                }
                movie.RatingCount -= 1;
            }

            _context.Movies.Update(movie);
            await _context.SaveChangesAsync();
        }
    }
}
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

        public async Task<IEnumerable<Movie>> GetMovies()
        {
            return await _context.Movies
                .Include(m => m.MovieGenres)
                    .ThenInclude(mg => mg.Genre)
                .Include(m => m.MovieCasts)
                    .ThenInclude(mc => mc.Cast)
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
    }
}
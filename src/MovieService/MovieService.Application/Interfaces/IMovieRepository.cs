using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MovieService.Domain.Entities;

namespace MovieService.Application.Interfaces
{
    public interface IMovieRepository
    {
        Task<IEnumerable<Movie>> GetMovies(int page, int pageSize);
        Task<Movie?> GetMovieById(Guid id);
        Task<IEnumerable<Movie>> GetMoviesByGenre(int genreId);
        Task<List<Genre>> GetGenres();

        Task UpdatedAverageRating(Guid movieId, int newScore, int oldScore, string eventType);

        Task<IEnumerable<Movie>> GetMoviesByIds(IEnumerable<Guid> movieIds);
    }
}
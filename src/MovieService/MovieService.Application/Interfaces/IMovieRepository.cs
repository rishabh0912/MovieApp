using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MovieService.Domain.Entities;

namespace MovieService.Application.Interfaces
{
    public interface IMovieRepository
    {
        Task<IEnumerable<Movie>> GetMovies();
        Task<Movie?> GetMovieById(Guid id);
        Task<IEnumerable<Movie>> GetMoviesByGenre(int genreId);
        Task<List<Genre>> GetGenres();
    }
}
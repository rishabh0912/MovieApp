using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MovieService.Application.DTOs;

namespace MovieService.Application.Interfaces
{
    public interface IMovieRepoService
    {
        Task<IEnumerable<MovieDto>> GetMovies(int page, int pageSize);
        Task<MovieDto?> GetMovieById(Guid id);
        Task<IEnumerable<MovieDto>> GetMoviesByGenre(int genreId);
        Task<List<GenreDto>> GetGenres();
        Task <IEnumerable<MovieDto?>> GetMoviesByIds(IEnumerable<Guid> movieIds);
    }
}
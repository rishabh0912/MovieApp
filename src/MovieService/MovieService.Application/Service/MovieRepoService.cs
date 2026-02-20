using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MovieService.Application.DTOs;
using MovieService.Application.Interfaces;

namespace MovieService.Application.Service
{
    public class MovieRepoService : IMovieRepoService
    {
        private readonly IMovieRepository _movieRepository;

        public MovieRepoService(IMovieRepository movieRepository)
        {
            _movieRepository = movieRepository;
        }
        public async Task<List<GenreDto>> GetGenres()
        {
            var genres = await _movieRepository.GetGenres();
            return genres
                .Select(g => new GenreDto
                {
                    Id = g.Id,
                    Name = g.Name
                })
                .ToList();            
        }

        public async Task<MovieDto?> GetMovieById(Guid id)
        {
            var movieDetails = await _movieRepository.GetMovieById(id);
            if (movieDetails == null)            
            {
                return null;
            }
            return new MovieDto            
            {
                Id = movieDetails.Id,
                Title = movieDetails.Title,
                Description = movieDetails.Description,
                ReleaseDate = movieDetails.ReleaseDate,
                PosterUrl = movieDetails.PosterUrl,
                AverageRating = movieDetails.AverageRating,
                Genres = movieDetails.MovieGenres.Select(mg => mg.Genre.Name).ToList(),
                Casts = movieDetails.MovieCasts.Select(mc => mc.Cast.Name).ToList()
            };
        }

        public async Task<IEnumerable<MovieDto>> GetMovies()
        {
            var movies = await _movieRepository.GetMovies();
            return movies.Select(m => new MovieDto
            {
                Id = m.Id,
                Title = m.Title,
                Description = m.Description,
                ReleaseDate = m.ReleaseDate,
                PosterUrl = m.PosterUrl,
                AverageRating = m.AverageRating,
                Genres = m.MovieGenres.Select(mg => mg.Genre.Name).ToList(),
                Casts = m.MovieCasts.Select(mc => mc.Cast.Name).ToList()
            })
            .ToList().AsEnumerable();
        }

        public async Task<IEnumerable<MovieDto>> GetMoviesByGenre(int genreId)
        {
            var moviesByGenre = await _movieRepository.GetMoviesByGenre(genreId);
            return moviesByGenre.Select(m => new MovieDto
            {
                Id = m.Id,
                Title = m.Title,
                Description = m.Description,
                ReleaseDate = m.ReleaseDate,
                PosterUrl = m.PosterUrl,
                AverageRating = m.AverageRating,
                Genres = m.MovieGenres.Select(mg => mg.Genre.Name).ToList(),
                Casts = m.MovieCasts.Select(mc => mc.Cast.Name).ToList()
            })
            .ToList().AsEnumerable();
        }
    }
}
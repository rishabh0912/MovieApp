using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MovieService.Application.Interfaces;

namespace MovieService.Api.Controller
{
    [ApiController]
    [Route("movies")]
    public class MovieController : ControllerBase
    {
        private readonly IMovieRepoService _movieRepoService;

        public MovieController(IMovieRepoService movieRepoService)
        {
            _movieRepoService = movieRepoService;
        }

        [HttpGet]
        public async Task<IActionResult> GetMovies(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 5)
        {
            var movies = await _movieRepoService.GetMovies(page, pageSize);
            return Ok(movies);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetMovieById(Guid id)
        {
            var movie = await _movieRepoService.GetMovieById(id);
            if (movie == null)
            {
                return NotFound();
            }
            return Ok(movie);
        }

        [HttpGet("genre")]
        public async Task<IActionResult> GetMoviesByGenre([FromQuery] int genreId)
        {
            var movies = await _movieRepoService.GetMoviesByGenre(genreId);
            return Ok(movies);
        }

        [HttpGet("genres")]
        public async Task<IActionResult> GetAllGenres()
        {
            var genres = await _movieRepoService.GetGenres();
            return Ok(genres);
        }

        [HttpGet("batch")]
        public async Task<IActionResult> GetMoviesByIds([FromQuery] IEnumerable<Guid> Ids)
        {
            var movies = await _movieRepoService.GetMoviesByIds(Ids);
            return Ok(movies);
        }
    }
}
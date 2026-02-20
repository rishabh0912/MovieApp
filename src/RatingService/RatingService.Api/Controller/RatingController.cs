using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RatingService.Application.DTOs;
using RatingService.Application.Interfaces;

namespace RatingService.Api.Controller
{
    [ApiController]
    [Route("rating")]
    public class RatingController : ControllerBase
    {
        private readonly IMovieRatingService _movieRatingService;

        public RatingController(IMovieRatingService movieRatingService)
        {
            _movieRatingService = movieRatingService;
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> AddRating([FromBody] RequestRatingDto ratingDto)
        {
            var result = await _movieRatingService.AddRating(ratingDto);
            return Ok(result);
        }

        [HttpGet]
        public async Task<IActionResult> GetRatingsByMovieId([FromQuery] List<Guid> movieIds)
        {
            var result = await _movieRatingService.GetRatingsByMovieId(movieIds);
            return Ok(result);
        }

        [HttpDelete("{ratingId}")]
        [Authorize]
        public async Task<IActionResult> DeleteRating(Guid ratingId)
        {
            await _movieRatingService.DeleteRating(ratingId);
            return NoContent();
        }

        [HttpPut("{ratingId}")]
        [Authorize]
        public async Task<IActionResult> UpdateRating(Guid ratingId, [FromBody] RequestRatingDto ratingDto)
        {
            if (ratingId != ratingDto.Id)
            {
                return BadRequest("Rating ID mismatch");
            }

            await _movieRatingService.UpdateRating(ratingDto);
            return NoContent();
        }
    }
}
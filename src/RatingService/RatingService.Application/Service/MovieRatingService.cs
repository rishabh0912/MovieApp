using System;
using System.Collections.Generic;      
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.VisualBasic;
using RatingService.Application.DTOs;
using RatingService.Application.Interfaces;
using RatingService.Domain.Entities;

namespace RatingService.Application.Service
{
    public class MovieRatingService : IMovieRatingService
    {
        private readonly IRatingRepository _ratingRepository;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public MovieRatingService(IRatingRepository ratingRepository,
            IHttpContextAccessor httpContextAccessor)
        {
            _ratingRepository = ratingRepository;
            _httpContextAccessor = httpContextAccessor;
        }
        public async Task<ResponseRatingDto> AddRating(RequestRatingDto ratingDto)
        {
          var userId = Guid.Parse(_httpContextAccessor.HttpContext!.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
          var newRating =   await _ratingRepository.AddRating(new Rating
            {
                MovieId = ratingDto.MovieId,
                UserId = userId,
                Score = ratingDto.Score,
                Review = ratingDto.Review,
                CreatedAt = DateTime.UtcNow
            });

            return new ResponseRatingDto
            {
                Id = newRating.Id,
                MovieId = newRating.MovieId,
                UserId = newRating.UserId,
                Score = newRating.Score,
                Review = newRating.Review,
                CreatedAt = newRating.CreatedAt,
                UpdatedAt = newRating.UpdatedAt
            };
        }

        public async Task DeleteRating(Guid ratingId)
        {
            await _ratingRepository.DeleteRating(ratingId);
        }

        public async Task<IEnumerable<ResponseRatingDto>> GetRatingsByMovieId(List<Guid> movieIds)
        {
            var result = new List<ResponseRatingDto>();
            foreach (var movieId in movieIds)
            {
                var ratings = await _ratingRepository.GetRatingsByMovieId(movieId);

                // Map to ResponseRatingDto and return
                result.AddRange(ratings.Select(r => new ResponseRatingDto
                {
                    Id = r.Id,
                    MovieId = r.MovieId,
                    UserId = r.UserId,
                    Score = r.Score,
                    Review = r.Review,
                    CreatedAt = r.CreatedAt,
                    UpdatedAt = r.UpdatedAt
                }).ToList());
            }
            return result;
        }

        public async Task UpdateRating(RequestRatingDto ratingDto)
        {
            var userId = Guid.Parse(_httpContextAccessor.HttpContext!.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var exisiting = await _ratingRepository.GetRatingsByMovieId(ratingDto.MovieId);
            if (exisiting == null)
            {
                throw new Exception("Rating not found");
            }
            await _ratingRepository.UpdateRating(new Rating
            {
                Id = ratingDto.Id.Value,
                UserId = userId,
                MovieId = ratingDto.MovieId,
                Score = ratingDto.Score,
                Review = ratingDto.Review,
                UpdatedAt = DateTime.UtcNow
            });
        }
    }
}
using System;
using System.Collections.Generic;      
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.AspNetCore.Http;
using Microsoft.VisualBasic;
using RatingService.Application.Clients;
using RatingService.Application.DTOs;
using RatingService.Application.Events;
using RatingService.Application.Interfaces;
using RatingService.Domain.Entities;

namespace RatingService.Application.Service
{
    public class MovieRatingService : IMovieRatingService
    {
        private readonly IRatingRepository _ratingRepository;
        private readonly IHttpContextAccessor _httpContextAccessor;

        private readonly MovieServiceClient _movieClient;

        private readonly IBus _bus; // <- Mass Transit built in publisher 

        public MovieRatingService(IRatingRepository ratingRepository,
            IHttpContextAccessor httpContextAccessor,
            IBus bus,
            MovieServiceClient movieClient)
        {
            _ratingRepository = ratingRepository;
            _httpContextAccessor = httpContextAccessor;
            _bus = bus;
            _movieClient = movieClient;
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

            await _bus.Publish(new RatingUpdatedEvent
            {
                MovieId = newRating.MovieId,
                NewScore = newRating.Score,
                OldScore = 0,
                EventType = "RatingCreated"
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

        public async Task<IEnumerable<ResponseRatingDto>> GetMoviesByUserId(int page, int pageSize)
        {
            var userId = Guid.Parse(_httpContextAccessor.HttpContext!.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var ratings = await _ratingRepository.GetMoviesByUserId(userId, page, pageSize);

            //Step 1 - Get all movieIds from the ratings
            var movieIds = ratings.Select(r =>r.MovieId).Distinct().ToList();

            //Step 2 - Call movie Service
            var movies = await _movieClient.GetMoviesByIds(movieIds);

            var movieDict = movies.ToDictionary(m => m.Id);

            var result = ratings.Select(r => new ResponseRatingDto
            {
                Id = r.Id,
                MovieId = r.MovieId,
                UserId = r.UserId,
                Score = r.Score,
                Review = r.Review,
                CreatedAt = r.CreatedAt,
                UpdatedAt = r.UpdatedAt,
                Movie = movieDict.GetValueOrDefault(r.MovieId)
            }).ToList();
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
            await _bus.Publish(new RatingUpdatedEvent
            {
                MovieId = ratingDto.MovieId,
                NewScore = ratingDto.Score,
                OldScore = exisiting.FirstOrDefault(r => r.UserId == userId)?.Score ?? 0,
                EventType = "RatingUpdated"
            });
            
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
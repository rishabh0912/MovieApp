using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using RatingService.Domain.Entities;

namespace RatingService.Application.Interfaces
{
    public interface IRatingRepository
    {
        Task<IEnumerable<Rating>> GetRatingsByMovieId(Guid movieId);
        Task<Rating> AddRating(Rating rating);

        Task UpdateRating(Rating rating);
        Task DeleteRating(Guid ratingId);
        Task<IEnumerable<Rating>> GetMoviesByUserId(Guid userId, int page, int pageSize);
    }
}
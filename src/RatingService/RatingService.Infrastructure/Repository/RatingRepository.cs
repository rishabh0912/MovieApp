using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using RatingService.Application.Interfaces;
using RatingService.Domain.Entities;
using RatingService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace RatingService.Infrastructure.Repository
{
    public class RatingRepository : IRatingRepository
    {
        private readonly AppDbContext _context;
        public RatingRepository(AppDbContext context)
        {
            _context = context;
        }
        
        public async Task<Rating> AddRating(Rating rating)
        {
            await _context.Ratings.AddAsync(rating);
            await _context.SaveChangesAsync(); 
            return rating;           
        }

        public async Task DeleteRating(Guid ratingId)
        {
            await _context.Ratings.Where(r => r.Id == ratingId)
                .ExecuteDeleteAsync();
        }

        public async Task<IEnumerable<Rating>> GetRatingsByMovieId(Guid movieId)
        {
            return await _context.Ratings
                .Where(r => r.MovieId == movieId)
                .ToListAsync();
        }

        public async Task UpdateRating(Rating rating)
        {
            _context.Ratings.Update(rating);
            await _context.SaveChangesAsync();
        }
    }
}
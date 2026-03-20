using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using RatingService.Application.DTOs;

namespace RatingService.Application.Interfaces
{
    public interface IMovieRatingService
    {
        Task<ResponseRatingDto> AddRating(RequestRatingDto ratingDto);
        Task UpdateRating(RequestRatingDto ratingDto);
        Task DeleteRating(Guid ratingId);
        Task<IEnumerable<ResponseRatingDto>> GetRatingsByMovieId(List<Guid> movieIds);
        Task<IEnumerable<ResponseRatingDto>> GetMoviesByUserId(int page, int pageSize);
    }
}
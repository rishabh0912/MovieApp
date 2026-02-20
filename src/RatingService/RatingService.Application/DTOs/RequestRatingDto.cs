using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RatingService.Application.DTOs
{
    public class RequestRatingDto
    {
        public Guid? Id { get; set; }
        public Guid MovieId { get; set; }
        public int Score { get; set; } // 1 to 5
        public string? Review { get; set; }
    }
}
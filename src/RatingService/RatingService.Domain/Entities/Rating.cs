using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RatingService.Domain.Entities
{
    public class Rating
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid MovieId { get; set; }
        public Guid UserId { get; set; }
        public int Score { get; set; } // 1 to 5
        public string? Review { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;  
        public DateTime UpdatedAt { get; set; }

    }
}
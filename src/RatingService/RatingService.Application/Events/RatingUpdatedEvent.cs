using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RatingService.Application.Events
{
    public class RatingUpdatedEvent
    {
        public Guid MovieId { get; set; }
        public int NewScore { get; set; }
        public int OldScore { get; set; }
        public string? EventType { get; set; }
    }
}
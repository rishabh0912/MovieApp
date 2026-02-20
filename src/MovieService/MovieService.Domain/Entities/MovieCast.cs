using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MovieService.Domain.Entities
{
    public class MovieCast
    {
        public Guid MovieId { get; set; }
        public Movie Movie { get; set; } = null!;
        public int CastId { get; set; }
        public Cast Cast { get; set; } = null!;
    }
}
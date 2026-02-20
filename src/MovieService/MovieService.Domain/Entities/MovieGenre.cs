using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MovieService.Domain.Entities
{
    public class MovieGenre
    {
        public Guid MovieId { get; set; }
        public Movie Movie { get; set; } = null!;

        public int GenreId { get; set; }
        public Genre Genre { get; set; } = null!;

    }
}
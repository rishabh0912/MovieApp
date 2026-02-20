using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MovieService.Domain.Entities
{
    public class Movie
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime ReleaseDate { get; set; }
        public string PosterUrl { get; set; } = string.Empty;

        public double AverageRating { get; set; } = 0.0;
        public int RatingCount { get; set; } = 0;

        public ICollection<MovieGenre> MovieGenres { get; set; } = new List<MovieGenre>();
        public ICollection<MovieCast> MovieCasts { get; set; } = new List<MovieCast>();
    }
}
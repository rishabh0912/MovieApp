using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MovieService.Application.DTOs
{
    public class MovieDto
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime ReleaseDate { get; set; }
        public string PosterUrl { get; set; } = string.Empty;
        public double AverageRating { get; set; }   
        public int TotalRatings { get; set; }
        public List<string> Genres { get; set; } = new List<string>();
        public List<string> Casts { get; set; } = new List<string>();
    }
}
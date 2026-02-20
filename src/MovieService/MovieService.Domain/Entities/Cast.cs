using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MovieService.Domain.Entities
{
    public class Cast
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;

        public ICollection<MovieCast> MovieCasts { get; set; } = new List<MovieCast>();
    }
}
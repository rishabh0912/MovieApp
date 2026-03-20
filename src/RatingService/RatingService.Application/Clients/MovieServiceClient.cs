using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Threading.Tasks;
using RatingService.Application.DTOs;

namespace RatingService.Application.Clients
{
    public class MovieServiceClient
    {
        private readonly HttpClient _httpClient;

        public MovieServiceClient(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<IEnumerable<MovieDto>> GetMoviesByIds(List<Guid> movieIds)
        {
            var queryString = string.Join("&", movieIds.Select(id => $"Ids={id}"));

            var response = await _httpClient.GetAsync($"/movies/batch?{queryString}");

            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<List<MovieDto>>() ?? new List<MovieDto>();
        }
    }
}
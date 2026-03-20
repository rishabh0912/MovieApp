using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.Extensions.Logging;
using MovieService.Application.Events;
using MovieService.Application.Interfaces;

namespace MovieService.Api.Consumers
{
    public class RatingUpdatedConsumer: IConsumer<RatingUpdatedEvent>
    {
        private readonly IMovieRepository _movieRepository;
        private readonly ILogger<RatingUpdatedConsumer> _logger;

        public RatingUpdatedConsumer(IMovieRepository movieRepository, ILogger<RatingUpdatedConsumer> logger)
        {
            _movieRepository = movieRepository;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<RatingUpdatedEvent> context)
        {
            var message = context.Message;
            _logger.LogInformation("Consumed RatingUpdatedEvent: MovieId={MovieId}, EventType={EventType}, NewScore={NewScore}, OldScore={OldScore}",
                message.MovieId, message.EventType, message.NewScore, message.OldScore);

            await _movieRepository.UpdatedAverageRating(message.MovieId, message.NewScore, message.OldScore, message.EventType);

            _logger.LogInformation("AverageRating updated for MovieId={MovieId}", message.MovieId);
        }
    }
}
RatingService                    RabbitMQ                    MovieService
   |                                |                              |
AddRating() ──── publish ──▶  [rating.updated]  ──── consume ──▶ UpdateAverageRating()
UpdateRating() ─ publish ──▶  [rating.updated]  ──── consume ──▶ UpdateAverageRating()
DeleteRating() ─ publish ──▶  [rating.deleted]  ──── consume ──▶ RecalculateAverageRating()

2. Shared Event contract - we have to create a same class (name also same) in movie and rating service
MassTransit matches by class name, not project reference so no shared project needed.

3. Install below packages in RatingService.API
   dotnet add package MassTransit
   dotnet add package MassTransit.RabbitMQ

4. Now in Rating service we need below changes
   1. We define the event structure - RatingUpdatedEvent.cs
   2. We did changes in MovieRatingService where we defining the ratings methods like AddRating, DeleteRating, UpdateRating.
   Here we need to inject IBus - provided by MassTransit
   IBus bus
   _bus = bus
   now just publish using _bus.Publish(RatingUpdatedEvent)
   3. Changes in program.cs where we need to register the MassTransit like below
   builder.Services.AddMassTransit(x =>
   x.UsingRabbitMq((ctx, cfg) =>
   {
      cfg.Hist(buil)
   })) --> check in program.cs



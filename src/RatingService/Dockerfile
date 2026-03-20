FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["RatingService.Api/RatingService.Api.csproj", "RatingService.Api/"]
COPY ["RatingService.Application/RatingService.Application.csproj", "RatingService.Application/"]
COPY ["RatingService.Domain/RatingService.Domain.csproj", "RatingService.Domain/"]
COPY ["RatingService.Infrastructure/RatingService.Infrastructure.csproj", "RatingService.Infrastructure/"]
RUN dotnet restore "RatingService.Api/RatingService.Api.csproj"

COPY . .
WORKDIR "/src/RatingService.Api"
RUN dotnet publish "RatingService.Api.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "RatingService.Api.dll"]
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["MovieService.Api/MovieService.Api.csproj", "MovieService.Api/"]
COPY ["MovieService.Application/MovieService.Application.csproj", "MovieService.Application/"]
COPY ["MovieService.Domain/MovieService.Domain.csproj", "MovieService.Domain/"]
COPY ["MovieService.Infrastructure/MovieService.Infrastructure.csproj", "MovieService.Infrastructure/"]
RUN dotnet restore "MovieService.Api/MovieService.Api.csproj"

COPY . .
WORKDIR "/src/MovieService.Api"
RUN dotnet publish "MovieService.Api.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "MovieService.Api.dll"]
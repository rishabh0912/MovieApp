# stage 1 - build
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["IdentityService.Api/IdentityService.Api.csproj", "IdentityService.Api/"]
COPY ["IdentityService.Application/IdentityService.Application.csproj", "IdentityService.Application/"]
COPY ["IdentityService.Domain/IdentityService.Domain.csproj", "IdentityService.Domain/"]
COPY ["IdentityService.Infrastructure/IdentityService.Infrastructure.csproj", "IdentityService.Infrastructure/"]
RUN dotnet restore "IdentityService.Api/IdentityService.Api.csproj"

COPY . .
WORKDIR "/src/IdentityService.Api"
RUN dotnet publish "IdentityService.Api.csproj" -c Release -o /app/publish

# stage 2 - runtime
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "IdentityService.Api.dll"]
# =============================================================================
# RoomToRentRadar.Api — Docker image for the ASP.NET Core MVC Web API (backend).
# Build from the REPOSITORY ROOT (the API needs Core + Infrastructure):
#   docker build -f RoomToRentRadar.Api/Dockerfile -t roomtorentradar-api .
# Normally started through docker-compose.yml at the repository root.
# =============================================================================

# ---------- Stage 1: build & publish with the .NET 10 SDK ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first, copying only project files, so Docker caches the NuGet layer
# and only re-downloads packages when a .csproj changes.
COPY global.json Directory.Build.props ./
COPY RoomToRentRadar.Core/RoomToRentRadar.Core.csproj RoomToRentRadar.Core/
COPY RoomToRentRadar.Infrastructure/RoomToRentRadar.Infrastructure.csproj RoomToRentRadar.Infrastructure/
COPY RoomToRentRadar.Api/RoomToRentRadar.Api.csproj RoomToRentRadar.Api/
RUN dotnet restore RoomToRentRadar.Api/RoomToRentRadar.Api.csproj

# Backend source only: the MAUI app and test projects are never part of this image.
COPY RoomToRentRadar.Core/ RoomToRentRadar.Core/
COPY RoomToRentRadar.Infrastructure/ RoomToRentRadar.Infrastructure/
COPY RoomToRentRadar.Api/ RoomToRentRadar.Api/
RUN dotnet publish RoomToRentRadar.Api/RoomToRentRadar.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

# ---------- Stage 2: small runtime image (no SDK, no source code) ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# SQLite file lives here; docker-compose mounts a named volume on it so data survives restarts.
RUN mkdir -p /app/data && chown app:app /app/data

COPY --from=build --chown=app:app /app/publish .

# The official image already listens on 8080 (ASPNETCORE_HTTP_PORTS=8080).
ENV ASPNETCORE_ENVIRONMENT=Production \
    Database__Path=/app/data/AppSQLite.db3
EXPOSE 8080

# Run as the built-in non-root "app" user.
# Jwt__SigningKey is NOT set here (never bake secrets into an image): pass it at run time.
USER app
ENTRYPOINT ["dotnet", "RoomToRentRadar.Api.dll"]

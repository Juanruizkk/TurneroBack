# ─── Stage 1: Build ──────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and project files first for layer caching
COPY src/Domain/TurneroApp.Domain.csproj src/Domain/
COPY src/Application/TurneroApp.Application.csproj src/Application/
COPY src/Infrastructure/TurneroApp.Infrastructure.csproj src/Infrastructure/
COPY src/Api/TurneroApp.Api.csproj src/Api/

# Restore only the API project (pulls all referenced projects; tests excluded)
RUN dotnet restore src/Api/TurneroApp.Api.csproj

# Copy all source files
COPY src/ src/

# Build and publish in Release mode
WORKDIR /src/src/Api
RUN dotnet publish TurneroApp.Api.csproj -c Release -o /app/publish --no-restore

# ─── Stage 2: Runtime ────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Create a non-root user for security
RUN addgroup --system appgroup && adduser --system --ingroup appgroup appuser
USER appuser

COPY --from=build /app/publish .

# Expose port (ASP.NET defaults to 8080 in .NET 8)
EXPOSE 8080

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "TurneroApp.Api.dll"]

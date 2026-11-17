# syntax=docker/dockerfile:1

# ---- build ----
FROM mcr.microsoft.com/dotnet/sdk:10.0.403-alpine3.24 AS build
WORKDIR /src

# Restore first (cached layer): solution-level props + project files + lock files only.
COPY global.json Directory.Build.props Directory.Packages.props TrackManagement.slnx ./
COPY src/TrackManagement.Domain/*.csproj src/TrackManagement.Domain/packages.lock.json src/TrackManagement.Domain/
COPY src/TrackManagement.Application/*.csproj src/TrackManagement.Application/packages.lock.json src/TrackManagement.Application/
COPY src/TrackManagement.Infrastructure/*.csproj src/TrackManagement.Infrastructure/packages.lock.json src/TrackManagement.Infrastructure/
COPY src/TrackManagement.Api/*.csproj src/TrackManagement.Api/packages.lock.json src/TrackManagement.Api/
RUN dotnet restore src/TrackManagement.Api/TrackManagement.Api.csproj --locked-mode

COPY src/ src/
RUN dotnet publish src/TrackManagement.Api/TrackManagement.Api.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

# ---- runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0.14-alpine3.24 AS runtime
# ICU for culture-aware string handling (Arabic text); alpine images ship without it.
RUN apk add --no-cache icu-libs
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
    ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_ENVIRONMENT=Production
WORKDIR /app
COPY --from=build --chown=$APP_UID:$APP_UID /app/publish .

# Non-root user provided by the official image.
USER $APP_UID
EXPOSE 8080

HEALTHCHECK --interval=15s --timeout=10s --start-period=30s --retries=5 \
  CMD wget -qO- http://localhost:8080/health > /dev/null || exit 1

ENTRYPOINT ["dotnet", "TrackManagement.Api.dll"]

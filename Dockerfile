# ── Build stage ──────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj/sln first for layer-cached restore
COPY Delta.DocView.sln ./
COPY src/Delta.DocView.Shared/Delta.DocView.Shared.csproj src/Delta.DocView.Shared/
COPY src/Delta.DocView.Server/Delta.DocView.Server.csproj src/Delta.DocView.Server/
COPY src/Delta.DocView.Client/Delta.DocView.Client.csproj src/Delta.DocView.Client/
RUN dotnet restore src/Delta.DocView.Server/Delta.DocView.Server.csproj

# Copy the rest and publish the server (pulls Client WASM + Shared)
COPY . .
RUN dotnet publish src/Delta.DocView.Server/Delta.DocView.Server.csproj \
    -c Release -o /app/publish /p:UseAppHost=false

# ── Runtime stage ────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
ENV DOCVIEW_LIBRARY_PATH=/data/step-library.json
EXPOSE 8080

# Run as the non-root user the aspnet image provides. The /data mount is a
# read-only, world-readable bind mount, so this non-root user can read the
# library file without issue.
USER $APP_UID

# No HEALTHCHECK: the aspnet:8.0 (Debian) base image does not reliably ship
# curl or wget, and installing a package solely for a healthcheck would bloat
# the image. The orchestrator/compose should probe the HTTP /health endpoint
# instead.

ENTRYPOINT ["dotnet", "Delta.DocView.Server.dll"]

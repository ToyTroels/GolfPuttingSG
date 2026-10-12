# Build from the repository root; publish only the API and its dependencies.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source
COPY global.json Directory.Build.props ./
COPY GolfSG.Api/ GolfSG.Api/
COPY src/GolfSG.Core/ src/GolfSG.Core/
COPY src/GolfSG.Application/ src/GolfSG.Application/
COPY src/GolfSG.Server.Infrastructure/ src/GolfSG.Server.Infrastructure/
RUN dotnet restore GolfSG.Api/GolfSG.Api.csproj --locked-mode
RUN dotnet publish GolfSG.Api/GolfSG.Api.csproj -c Release --no-restore -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./
ENV ASPNETCORE_ENVIRONMENT=Production
USER $APP_UID
EXPOSE 8080
# Render supplies PORT; local Docker runs default to 8080.
ENTRYPOINT ["/bin/sh", "-c", "exec dotnet GolfSG.Api.dll --urls http://0.0.0.0:${PORT:-8080}"]

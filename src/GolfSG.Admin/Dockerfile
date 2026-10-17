# Build with repository root as context:
# docker build -f src/GolfSG.Admin/Dockerfile -t golfsg-admin .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source
COPY global.json Directory.Build.props ./
COPY build/AdminAccess.cs build/
COPY src/GolfSG.Admin/ src/GolfSG.Admin/
COPY src/GolfSG.Application/Server/AdminDirectoryPage.cs src/GolfSG.Application/Server/
COPY src/GolfSG.Application/Server/PersonalDataPage.cs src/GolfSG.Application/Server/
COPY src/GolfSG.Application/Server/PersonalPerformance.cs src/GolfSG.Application/Server/
RUN dotnet restore src/GolfSG.Admin/GolfSG.Admin.csproj --locked-mode
RUN dotnet publish src/GolfSG.Admin/GolfSG.Admin.csproj -c Release --no-restore -o /app/publish /p:UseAppHost=false
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./
ENV ASPNETCORE_ENVIRONMENT=Staging
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["/bin/sh", "-c", "exec dotnet GolfSG.Admin.dll --urls http://0.0.0.0:${PORT:-8080}"]

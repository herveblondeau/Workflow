# Build context is the repo root (Main.Api references Core and Infrastructure
# via relative ProjectReference paths).
#
# Stage 1: build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Core/Core.csproj Core/
COPY Infrastructure/Infrastructure.csproj Infrastructure/
COPY Main.Api/Main.Api.csproj Main.Api/
RUN dotnet restore Main.Api/Main.Api.csproj
COPY Core/ Core/
COPY Infrastructure/ Infrastructure/
COPY Main.Api/ Main.Api/
RUN dotnet publish Main.Api/Main.Api.csproj -c Release -o /app/publish

# Stage 2: runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
RUN mkdir -p /app/storage
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "Main.Api.dll"]

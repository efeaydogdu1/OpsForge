FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY backend/OpsForge.sln backend/
COPY backend/src ./backend/src
COPY backend/tests ./backend/tests

WORKDIR /src/backend
RUN dotnet restore OpsForge.sln
RUN dotnet publish src/OpsForge.Api/OpsForge.Api.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "OpsForge.Api.dll"]

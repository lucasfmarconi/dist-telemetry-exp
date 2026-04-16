# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0-noble AS build
WORKDIR /src

COPY ["src/Orchestrator/Orchestrator.csproj", "src/Orchestrator/"]
COPY . .

RUN dotnet restore "src/Orchestrator/Orchestrator.csproj"
RUN dotnet publish "src/Orchestrator/Orchestrator.csproj" -c Release -o /app/publish --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/runtime:10.0-noble-chiseled AS final
WORKDIR /app

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "Orchestrator.dll"]
# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0-noble AS build
WORKDIR /src

COPY ["src/TelemetryApi/TelemetryApi.csproj", "src/TelemetryApi/"]
COPY . .

RUN dotnet restore "src/TelemetryApi/TelemetryApi.csproj"
RUN dotnet publish "src/TelemetryApi/TelemetryApi.csproj" -c Release -o /app/publish --no-restore
RUN mkdir -p /app/publish/data

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080

COPY --chown=app:app --from=build /app/publish .

EXPOSE 8080
VOLUME ["/app/data"]

ENTRYPOINT ["dotnet", "TelemetryApi.dll"]

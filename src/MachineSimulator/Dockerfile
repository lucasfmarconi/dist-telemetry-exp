# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0-noble AS build
WORKDIR /src

COPY ["src/MachineSimulator/MachineSimulator.csproj", "src/MachineSimulator/"]
COPY . .

RUN dotnet restore "src/MachineSimulator/MachineSimulator.csproj"
RUN dotnet publish "src/MachineSimulator/MachineSimulator.csproj" -c Release -o /app/publish --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/runtime:10.0-noble-chiseled AS final
WORKDIR /app

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "MachineSimulator.dll"]
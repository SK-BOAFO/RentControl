# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["RentControlSystem.csproj", "./"]
RUN dotnet restore "RentControlSystem.csproj"

COPY . .
RUN dotnet publish "RentControlSystem.csproj" -c Release -o /app/publish \
    --no-restore \
    -p:UseAppHost=false \
    -p:DebugType=None \
    -p:DebugSymbols=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final

RUN adduser --disabled-password --gecos "" appuser

WORKDIR /app
COPY --from=build --chown=appuser:appuser /app/publish .

USER appuser

# Critical for Render – bind to their PORT env var
ENV ASPNETCORE_URLS=http://+:${PORT:-10000}

EXPOSE 10000

ENTRYPOINT ["dotnet", "RentControlSystem.dll"]

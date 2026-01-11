# =====================================
# STAGE 1: Build & Publish
# =====================================
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /src

# Copy only the project file first (excellent NuGet caching)
COPY ["RentControlSystem.csproj", "./"]

# Restore dependencies
RUN dotnet restore "RentControlSystem.csproj"

# Copy everything else
COPY . .

# Publish optimized for production/container
RUN dotnet publish "RentControlSystem.csproj" \
    -c Release \
    -o /app/publish \
    --no-restore \
    -p:UseAppHost=false \
    -p:DebugType=None \
    -p:DebugSymbols=false

# =====================================
# STAGE 2: Runtime (small & secure)
# =====================================
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final

# Create non-root user
RUN adduser --disabled-password --gecos "" appuser

WORKDIR /app

# Copy published files with correct ownership
COPY --from=build --chown=appuser:appuser /app/publish .

USER appuser

# Required for Render.com (port 10000)
ENV ASPNETCORE_URLS=http://+:10000

EXPOSE 10000

ENTRYPOINT ["dotnet", "RentControlSystem.dll"]

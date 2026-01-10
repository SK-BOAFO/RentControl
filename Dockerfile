# =====================================
# STAGE 1: Build & Publish
# =====================================
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /src

# Copy project file first → excellent caching for NuGet restore
COPY ["RentControlSystem.csproj", "./"]

# Restore packages (this layer caches beautifully)
RUN dotnet restore "RentControlSystem.csproj"

# Copy the full source
COPY . .

# Publish optimized for production
RUN dotnet publish "RentControlSystem.csproj" \
    -c Release \
    -o /app/publish \
    --no-restore \
    -p:UseAppHost=false \
    -p:DebugType=None \
    -p:DebugSymbols=false

# =====================================
# STAGE 2: Runtime (small, secure, production-ready)
# =====================================
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final

# Create non-root user (security best practice on Render)
RUN adduser --disabled-password --gecos "" appuser

WORKDIR /app

# Copy only published files with proper ownership
COPY --from=build --chown=appuser:appuser /app/publish .

# Run as non-root
USER appuser

# Critical for Render: listen on their expected port
ENV ASPNETCORE_URLS=http://+:10000

# Expose Render's port
EXPOSE 10000

# Start the app
ENTRYPOINT ["dotnet", "RentControlSystem.dll"]

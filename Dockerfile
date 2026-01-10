# =====================================
# STAGE 1: Build & Publish
# =====================================
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build

WORKDIR /src

# Copy only the project file first (great for caching NuGet restore)
COPY ["RentControlSystem.csproj", "./"]

# Restore dependencies (this layer caches very well)
RUN dotnet restore "RentControlSystem.csproj"

# Now copy the entire source code
COPY . .

# Publish the application (Release, optimized, no host)
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
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final

# Create non-root user (security best practice)
RUN adduser --disabled-password --gecos "" appuser

WORKDIR /app

# Copy published output with correct ownership
COPY --from=build --chown=appuser:appuser /app/publish .

# Run as non-root
USER appuser

# Render.com requires port 10000 (critical!)
ENV ASPNETCORE_URLS=http://+:10000

# Expose the port Render expects
EXPOSE 10000

# Start the application
ENTRYPOINT ["dotnet", "RentControlSystem.dll"]

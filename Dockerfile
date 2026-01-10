# =====================================
# STAGE 1: Build & Publish
# =====================================
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy the project file first → great caching for dependencies
COPY ["RentControlSystem.csproj", "./"]

# Restore NuGet packages (cached layer)
RUN dotnet restore "RentControlSystem.csproj"

# Copy the rest of the source code
COPY . .

# Publish the app (Release configuration, optimized)
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

# Create non-root user for security (best practice)
RUN adduser --disabled-password --gecos "" appuser

WORKDIR /app

# Copy only the published output (with correct ownership)
COPY --from=build --chown=appuser:appuser /app/publish .

# Switch to non-root user
USER appuser

# Render.com requires the app to listen on port 10000
ENV ASPNETCORE_URLS=http://+:10000

# Expose the port Render uses
EXPOSE 10000

# Entry point - run the app
ENTRYPOINT ["dotnet", "RentControlSystem.dll"]

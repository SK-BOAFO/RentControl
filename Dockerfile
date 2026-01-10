# ========================
# STAGE 1: Build
# ========================
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Cache NuGet packages (huge speed improvement on rebuilds)
COPY ["*.sln", "./"]
COPY ["src/MyApp.WebApi/MyApp.WebApi.csproj", "src/MyApp.WebApi/"]
COPY ["src/MyApp.Application/MyApp.Application.csproj", "src/MyApp.Application/"]
COPY ["src/MyApp.Domain/MyApp.Domain.csproj", "src/MyApp.Domain/"]
COPY ["src/MyApp.Infrastructure/MyApp.Infrastructure.csproj", "src/MyApp.Infrastructure/"]

# Restore once → cache layer
RUN dotnet restore "src/MyApp.WebApi/MyApp.WebApi.csproj"

# Copy everything else and build
COPY . .
WORKDIR "/src/src/MyApp.WebApi"
RUN dotnet publish -c Release -o /app/publish \
    --no-restore \
    -p:UseAppHost=false \
    -p:DebugType=None \
    -p:DebugSymbols=false

# ========================
# STAGE 2: Runtime (very small!)
# ========================
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final

# Security: create non-root user (very important!)
RUN adduser --disabled-password --gecos "" appuser

WORKDIR /app

# Copy only the published output
COPY --from=build --chown=appuser:appuser /app/publish .

# Run as non-root user
USER appuser

# Expose port (change if needed)
EXPOSE 8080

# Modern .NET apps usually listen on 8080 in containers
ENV ASPNETCORE_URLS=http://+:8080

# Health checks (optional but recommended)
HEALTHCHECK --interval=30s --timeout=3s --retries=3 \
    CMD curl --fail http://localhost:8080/health || exit 1

# Entry point - use exec form
ENTRYPOINT ["dotnet", "MyApp.WebApi.dll"]

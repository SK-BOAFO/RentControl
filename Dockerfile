# =====================================
# STAGE 1: Build
# =====================================
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Cache NuGet packages - copy project files first
COPY ["*.sln", "./"]
COPY ["src/RentControl.WebApi/RentControl.WebApi.csproj", "src/RentControl.WebApi/"]
# ... copy other .csproj files if you have multiple projects

RUN dotnet restore "src/RentControl.WebApi/RentControl.WebApi.csproj"

COPY . .
WORKDIR "/src/src/RentControl.WebApi"

# Publish - optimized for container
RUN dotnet publish "RentControl.WebApi.csproj" -c Release -o /app/publish \
    --no-restore \
    -p:UseAppHost=false \
    -p:DebugType=None \
    -p:DebugSymbols=false

# =====================================
# STAGE 2: Runtime - Production
# =====================================
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final

# Security: non-root user
RUN addgroup --system --gid 1000 appgroup && \
    adduser --system --uid 1000 --ingroup appgroup appuser

WORKDIR /app
COPY --from=build --chown=appuser:appgroup /app/publish .

USER appuser

# Modern cloud port convention (change to 10000 for Render, 80/443 for others)
ENV ASPNETCORE_URLS=http://+:8080

EXPOSE 8080

# Optional but recommended health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD curl --fail http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "RentControl.WebApi.dll"]

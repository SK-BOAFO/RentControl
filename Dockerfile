# STAGE 1: Build & Publish
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /src

COPY ["RentControlSystem.csproj", "./"]

RUN dotnet restore "RentControlSystem.csproj"

COPY . .

# Optional workaround if bad cache persists (rare after cleaning bin/obj)
# RUN mkdir -p "/Program Files (x86)/Microsoft Visual Studio/Shared/NuGetPackages"

RUN dotnet publish "RentControlSystem.csproj" \
    -c Release \
    -o /app/publish \
    --no-restore \
    -p:UseAppHost=false \
    -p:DebugType=None \
    -p:DebugSymbols=false

# STAGE 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final

RUN adduser --disabled-password --gecos "" appuser

WORKDIR /app
COPY --from=build --chown=appuser:appuser /app/publish .

USER appuser

ENV ASPNETCORE_URLS=http://+:10000
EXPOSE 10000

ENTRYPOINT ["dotnet", "RentControlSystem.dll"]

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["*.sln", "./"]
COPY ["src/RentControl.WebApi/RentControl.WebApi.csproj", "src/RentControl.WebApi/"]
COPY ["src/RentControl.Application/RentControl.Application.csproj", "src/RentControl.Application/"]
COPY ["src/RentControl.Domain/RentControl.Domain.csproj", "src/RentControl.Domain/"]
COPY ["src/RentControl.Infrastructure/RentControl.Infrastructure.csproj", "src/RentControl.Infrastructure/"]

RUN dotnet restore "src/RentControl.WebApi/RentControl.WebApi.csproj"

COPY . .
WORKDIR "/src/src/RentControl.WebApi"
RUN dotnet publish "RentControl.WebApi.csproj" -c Release -o /app/publish \
    --no-restore \
    -p:UseAppHost=false

# Runtime stage same as above...
FROM mcr.microsoft.com/dotnet/aspnet:9.0
# ... (keep the rest)

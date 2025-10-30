FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution and project files
COPY ["UserService/UserService.csproj", "UserService/"]
COPY ["UserService/nuget.config", "UserService/"]
COPY ["Shared/Shared.csproj", "Shared/"]

# Restore dependencies
RUN dotnet restore "UserService/UserService.csproj"

# Copy source code
COPY ["UserService/", "UserService/"]
COPY ["Shared/", "Shared/"]

# Build and publish
WORKDIR /src/UserService
RUN dotnet publish -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:80
EXPOSE 80

# Install curl for health checks
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish ./
ENTRYPOINT ["dotnet", "UserService.dll"]

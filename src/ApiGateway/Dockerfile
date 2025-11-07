FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["ApiGateway/ApiGateway.csproj", "ApiGateway/"]
COPY ["ApiGateway/nuget.config", "ApiGateway/"]
COPY ["Shared/Shared.csproj", "Shared/"]

RUN dotnet restore "ApiGateway/ApiGateway.csproj"

COPY ["ApiGateway/", "ApiGateway/"]
COPY ["Shared/", "Shared/"]

WORKDIR /src/ApiGateway
RUN dotnet publish -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:80
EXPOSE 80

# Install curl for health checks
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish ./
ENTRYPOINT ["dotnet", "ApiGateway.dll"]

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["InventoryService/InventoryService.csproj", "InventoryService/"]
COPY ["Shared/Shared.csproj", "Shared/"]

RUN dotnet restore "InventoryService/InventoryService.csproj"

COPY ["InventoryService/", "InventoryService/"]
COPY ["Shared/", "Shared/"]

WORKDIR /src/InventoryService
RUN dotnet publish -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:80
EXPOSE 80

# Install curl for health checks
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish ./
ENTRYPOINT ["dotnet", "InventoryService.dll"]

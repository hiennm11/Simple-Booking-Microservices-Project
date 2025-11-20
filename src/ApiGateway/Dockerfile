# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files and restore dependencies as distinct layers for better caching
COPY ["ApiGateway/ApiGateway.csproj", "ApiGateway/"]
COPY ["ApiGateway/nuget.config", "ApiGateway/"]
COPY ["Shared/Shared.csproj", "Shared/"]

RUN dotnet restore "ApiGateway/ApiGateway.csproj"

# Copy source code
COPY ["ApiGateway/", "ApiGateway/"]
COPY ["Shared/", "Shared/"]

# Build and publish with optimizations
WORKDIR /src/ApiGateway
RUN dotnet publish -c Release -o /app/publish \
    -p:DebugType=None \
    -p:DebugSymbols=false

# Runtime stage - minimal runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

# Add metadata labels
LABEL maintainer="Booking System Team" \
      service="api-gateway" \
      version="1.0"

WORKDIR /app

# Install curl for health checks in a single layer and clean up
RUN apt-get update && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/* \
    && groupadd -r appgroup && useradd -r -g appgroup appuser \
    && chown -R appuser:appgroup /app

# Set environment variables
ENV ASPNETCORE_URLS=http://+:80 \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

EXPOSE 80

# Copy published app from build stage
COPY --from=build --chown=appuser:appgroup /app/publish ./

# Switch to non-root user
USER appuser

ENTRYPOINT ["dotnet", "ApiGateway.dll"]

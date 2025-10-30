FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# copy csproj and nuget.config then restore as distinct layers
COPY *.csproj nuget.config ./
ENV NUGET_PACKAGES=/root/.nuget/packages
ENV NUGET_FALLBACK_PACKAGES=/root/.nuget/fallback
RUN mkdir -p /root/.nuget/packages /root/.nuget/fallback \
	&& dotnet restore

# copy everything else and build
COPY . ./
RUN dotnet publish -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:80
EXPOSE 80

# Install curl for health checks
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish ./
ENTRYPOINT ["dotnet", "ApiGateway.dll"]

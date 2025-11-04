FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["PaymentService/PaymentService.csproj", "PaymentService/"]
COPY ["PaymentService/nuget.config", "PaymentService/"]
COPY ["Shared/Shared.csproj", "Shared/"]

RUN dotnet restore "PaymentService/PaymentService.csproj"

COPY ["PaymentService/", "PaymentService/"]
COPY ["Shared/", "Shared/"]

WORKDIR /src/PaymentService
RUN dotnet publish -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:80
EXPOSE 80

# Install curl for health checks
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish ./
ENTRYPOINT ["dotnet", "PaymentService.dll"]

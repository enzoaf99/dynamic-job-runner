FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
ENV ASPNETCORE_URLS=http://+:80
ENV ASPNETCORE_ENVIRONMENT=Development

RUN apt-get update && \
    apt-get install -y iputils-ping curl wget && \
    rm -rf /var/lib/apt/lists/*

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["DynamicJobRunnerApp/", "."]
RUN dotnet restore
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ConnectionStrings__Default="Host=${POSTGRES_HOST:-db};Database=${POSTGRES_DB:-jobrunner};Username=${POSTGRES_USER:-postgres};Password=${POSTGRES_PASSWORD:-yourpass}"

ENTRYPOINT ["dotnet", "DynamicJobRunnerApp.dll"]
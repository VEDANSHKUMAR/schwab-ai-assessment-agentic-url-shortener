# Official Microsoft .NET images: https://hub.docker.com/_/microsoft-dotnet
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY UrlShortener.sln ./
COPY src/ ./src/
COPY tests/ ./tests/
COPY client/ ./client/
RUN apt-get update && apt-get install -y --no-install-recommends nodejs npm \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /src/client
RUN npm ci || npm install
RUN npm run build
WORKDIR /src
RUN dotnet restore UrlShortener.sln
RUN dotnet publish src/UrlShortener.Api/UrlShortener.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
COPY --from=build /src/client/dist /app/client-dist
ENV ASPNETCORE_URLS=http://+:5488
ENV Client__DistPath=/app/client-dist
EXPOSE 5488
ENTRYPOINT ["dotnet", "UrlShortener.Api.dll"]

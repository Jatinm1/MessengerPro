# Stage 1 - Build
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish ChatApp.Api/ChatApp.Api.csproj -c Release -o /app/publish

# Stage 2 - Runtime
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app

# Railway dynamic port
ENV ASPNETCORE_URLS=http://+:$PORT

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ChatApp.Api.dll"]
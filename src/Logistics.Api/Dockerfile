# Build context: repository root (see docker-compose.yml).

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore first, from project files only, so the layer is cached until a dependency changes.
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/Logistics.Domain/Logistics.Domain.csproj src/Logistics.Domain/
COPY src/Logistics.Application/Logistics.Application.csproj src/Logistics.Application/
COPY src/Logistics.Infrastructure/Logistics.Infrastructure.csproj src/Logistics.Infrastructure/
COPY src/Logistics.Api/Logistics.Api.csproj src/Logistics.Api/
RUN dotnet restore src/Logistics.Api/Logistics.Api.csproj

COPY src/ src/
RUN dotnet publish src/Logistics.Api/Logistics.Api.csproj -c Release -o /app --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app .

# Non-root user that ships with the .NET 8 images.
USER $APP_UID
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Logistics.Api.dll"]

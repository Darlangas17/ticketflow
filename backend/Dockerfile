# syntax=docker/dockerfile:1

# ---------- Build ----------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore first, in its own layer, so code changes do not re-download packages.
COPY Directory.Build.props ./
COPY src/TicketFlow.Domain/TicketFlow.Domain.csproj src/TicketFlow.Domain/
COPY src/TicketFlow.Application/TicketFlow.Application.csproj src/TicketFlow.Application/
COPY src/TicketFlow.Infrastructure/TicketFlow.Infrastructure.csproj src/TicketFlow.Infrastructure/
COPY src/TicketFlow.Api/TicketFlow.Api.csproj src/TicketFlow.Api/
RUN dotnet restore src/TicketFlow.Api/TicketFlow.Api.csproj

COPY src/ src/
RUN dotnet publish src/TicketFlow.Api/TicketFlow.Api.csproj \
    --configuration Release --no-restore --output /app/publish /p:UseAppHost=false

# ---------- Runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# The official image ships a non-root "app" user; never run the API as root.
USER $APP_UID
ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_gcServer=false
EXPOSE 8080

ENTRYPOINT ["dotnet", "TicketFlow.Api.dll"]

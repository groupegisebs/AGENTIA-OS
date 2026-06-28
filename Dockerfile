# ── Build stage ────────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

COPY AgenticFactory.slnx ./
COPY src/AgenticFactory.Shared/AgenticFactory.Shared.csproj             src/AgenticFactory.Shared/
COPY src/AgenticFactory.Domain/AgenticFactory.Domain.csproj             src/AgenticFactory.Domain/
COPY src/AgenticFactory.Application/AgenticFactory.Application.csproj   src/AgenticFactory.Application/
COPY src/AgenticFactory.Infrastructure/AgenticFactory.Infrastructure.csproj src/AgenticFactory.Infrastructure/
COPY src/AgenticFactory.Api/AgenticFactory.Api.csproj                   src/AgenticFactory.Api/
COPY src/AgenticFactory.Web/AgenticFactory.Web.csproj                   src/AgenticFactory.Web/
COPY src/AgenticFactory.Runtime.WindowsService/AgenticFactory.Runtime.WindowsService.csproj \
     src/AgenticFactory.Runtime.WindowsService/
COPY tests/AgenticFactory.Tests/AgenticFactory.Tests.csproj             tests/AgenticFactory.Tests/

RUN dotnet restore AgenticFactory.slnx

COPY . .

RUN dotnet publish src/AgenticFactory.Api/AgenticFactory.Api.csproj \
        -c Release -o /out/api --no-restore

RUN dotnet publish src/AgenticFactory.Web/AgenticFactory.Web.csproj \
        -c Release -o /out/web --no-restore

RUN dotnet publish src/AgenticFactory.Runtime.WindowsService/AgenticFactory.Runtime.WindowsService.csproj \
        -c Release -o /out/runtime --no-restore


# ── Runtime stage (API) ─────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_URLS=http://+:8080 \
    DOTNET_RUNNING_IN_CONTAINER=true

WORKDIR /app

RUN apt-get update && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /out/api .

HEALTHCHECK --interval=15s --timeout=5s --start-period=30s --retries=3 \
    CMD curl -fsS http://localhost:8080/health || exit 1

EXPOSE 8080

ENTRYPOINT ["dotnet", "AgenticFactory.Api.dll"]

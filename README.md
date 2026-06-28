# Agentic Factory (.NET)

Socle **Clean/Modular Monolith** pour une Agentic Factory multi-tenant: API + UI admin + runtime Windows Service.

## Structure

```text
/src
  /AgenticFactory.Web
  /AgenticFactory.Api
  /AgenticFactory.Application
  /AgenticFactory.Domain
  /AgenticFactory.Infrastructure
  /AgenticFactory.Runtime.WindowsService
  /AgenticFactory.Shared
/tests
  /AgenticFactory.Tests
```

## Stack implémentée

- ASP.NET Core (actuellement `net10.0` dans ce repo; net9 non disponible sur la machine)
- C#, EF Core, Identity
- PostgreSQL par défaut (SQL Server optionnel via env)
- MVC (dashboard/admin) + Web API
- Worker Service Windows (`BackgroundService`)
- SignalR (`/hubs/runs`)
- Serilog + Swagger (dev) + OpenTelemetry baseline
- Mode IA mock garanti bout-en-bout (`AI:Mode=mock`)

## Variables d'environnement

- `DATABASE_PROVIDER` = `postgres` (default) | `sqlserver` | `inmemory`
- `DATABASE_CONNECTION_STRING` = chaîne provider correspondante
- `ASPNETCORE_ENVIRONMENT` = `Development` (dev defaults incluent `inmemory`)
- `Auth__JwtKey`, `Auth__Issuer`, `Auth__Audience`
- `AI__Mode` = `mock` | `provider`
- `AI__Provider` = `mock` | `openai` | `azureopenai`
- `AI__OpenAI__ApiKey`, `AI__OpenAI__Model`, `AI__OpenAI__Endpoint`
- `AI__AzureOpenAI__ApiKey`, `AI__AzureOpenAI__Endpoint`, `AI__AzureOpenAI__Deployment`, `AI__AzureOpenAI__ApiVersion`
- `OPENAI_API_KEY` / `AZURE_OPENAI_API_KEY` (fallback env vars si clés absentes en config)
- `AI__Pricing__PromptPer1kUsd`, `AI__Pricing__CompletionPer1kUsd` (estimation coût)

### Comportement provider IA

- `AI:Mode=mock` force le provider mock.
- `AI:Mode=provider` active le provider réel (`AI:Provider`).
- En cas de configuration incomplète, clé absente, timeout ou erreur API provider, le runtime bascule automatiquement sur le fallback mock (`usedFallback=true` dans la sortie de run).

## Lancement

```bash
dotnet build AgenticFactory.slnx
dotnet test AgenticFactory.slnx
```

### API

```bash
dotnet run --project src/AgenticFactory.Api
```

- Swagger en dev: `/swagger`
- Health: `/health`

### Web Admin

```bash
dotnet run --project src/AgenticFactory.Web
```

- Login seed: `admin@agenticfactory.local` / `Admin123$ChangeMe`

### Runtime Service (console/dev)

```bash
dotnet run --project src/AgenticFactory.Runtime.WindowsService
```

## Migration EF

Migration initiale créée dans `src/AgenticFactory.Infrastructure/Persistence/Migrations`.

Pour regénérer:

```bash
dotnet ef migrations add InitialCreate \
  --project src/AgenticFactory.Infrastructure/AgenticFactory.Infrastructure.csproj \
  --startup-project src/AgenticFactory.Api/AgenticFactory.Api.csproj \
  --output-dir Persistence/Migrations
```

## Endpoints clés

- `POST /api/auth/register`
- `POST /api/auth/login`
- `POST /api/agent-creation/chat`
- `POST /api/agent-creation/validate`
- `POST /api/agents/deploy`
- `POST /api/agents/{endpointSlug}/invoke` (`X-Agent-Key` requis)
- `GET /api/monitoring/dashboard`

## Limites phase actuelle

- Cron scheduler volontairement basique (`*`, `*/n`, valeur fixe sur 5 champs)
- Pas de retry/backoff avancé côté provider IA
- Dashboard orienté monitoring opérationnel (pas de drill-down historique avancé)
- Runtime health locale via heartbeat DB uniquement

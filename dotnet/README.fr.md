# AGENTIA-OS .NET - Phase 1

Ce dossier contient un nouveau socle backend en .NET/C# (ASP.NET Core 8), cree a part de zero, sans supprimer ni modifier la base Python existante.

## Prerequis

- .NET SDK 8+ (teste avec SDK 10, cible `net8.0`)

## Lancer le projet

Depuis la racine du repo:

```powershell
dotnet restore dotnet/AgentiaOs.slnx
dotnet build dotnet/AgentiaOs.slnx
dotnet run --project dotnet/src/AgentiaOs.Api
```

API disponible par defaut sur:

- `http://localhost:5000` (selon profil local)
- Swagger: `http://localhost:5000/swagger`

## Configuration

Configuration par `appsettings.json` + variables d'environnement:

- `ConnectionStrings__Default` (SQLite)
- `Jwt__Issuer`
- `Jwt__Audience`
- `Jwt__SigningKey`
- `Jwt__ExpirationMinutes`

Exemple:

```powershell
$env:ConnectionStrings__Default="Data Source=agentiaos.custom.db"
$env:Jwt__SigningKey="ChangeThisInRealEnv_WithLongRandomSecret"
dotnet run --project dotnet/src/AgentiaOs.Api
```

## Endpoints cles (MVP)

- `GET /health`
- `POST /api/auth/register`
- `POST /api/auth/login`
- `POST /api/conversations`
- `GET /api/conversations`
- `GET /api/conversations/{conversationId}`
- `POST /api/conversations/{conversationId}/messages`
- `GET /api/blueprint`
- `POST /api/deploy`
- `GET /api/deploy/{deploymentId}`

## Tests

```powershell
dotnet test dotnet/AgentiaOs.slnx
```

Tests inclus:

- health check
- auth register/login
- conversation happy path (create + message + get)

## Limites phase 1

- Auth simple JWT sans gestion de roles/refresh token.
- Pas d'orchestration AI reelle (reponse message mock).
- Deploy simule (`pending` vers `succeeded` apres delai).
- Pas de migrations EF Core versionnees (usage `EnsureCreated`).
- Validation metier encore minimale.

## Phase 2 suggeree

- Ajouter migrations EF Core + versionning schema.
- Introduire cas d'usage Application (CQRS ou services metier).
- Renforcer securite (refresh token, rotation secrets, roles).
- Brancher moteur conversation/agent reel.
- Ajouter observabilite et tests e2e supplementaires.

# Secrets GitHub Actions — Agentia OS

**Ne jamais committer de secrets dans le dépôt.** Toutes les valeurs sensibles passent par les **Secrets** du repository GitHub (Settings → Secrets and variables → Actions).

## Secrets obligatoires (production)

| Secret GitHub | Variable `.env` | Description |
|---------------|-----------------|-------------|
| `AGENTIA_OS_DATABASE_URL` | `DATABASE_URL` | PostgreSQL async, ex. `postgresql+asyncpg://user:pass@host:5432/agentia` |
| `AGENTIA_OS_JWT_SECRET` | `JWT_SECRET` | Chaîne aléatoire longue (≥ 32 caractères) |
| `AGENTIA_OS_GEMINI_API_KEY` | `GEMINI_API_KEY` | Clé API Google Gemini |

## Secrets recommandés

| Secret GitHub | Variable `.env` |
|---------------|-----------------|
| `AGENTIA_OS_GISEBS_PAY_API_KEY` | `GISEBS_PAY_API_KEY` |
| `AGENTIA_OS_GISEBS_PAY_GATEWAY_URL` | `GISEBS_PAY_GATEWAY_URL` |
| `AGENTIA_OS_SSH_PRIVATE_KEY` | *(déploiement SSH)* |

## Variables non sensibles (GitHub Variables)

Peuvent être définies dans **Variables** (pas Secrets) :

- `AGENTIA_OS_APP_ROOT`, `AGENTIA_OS_SERVICE_NAME`, `AGENTIA_OS_LISTEN_PORT`
- `GISEBS_PAY_APP_CODE`, URLs de succès/annulation

## Développement local

1. Copier `.env.example` → `.env` (`.env` est dans `.gitignore`)
2. Renseigner localement `DATABASE_URL`, `GEMINI_API_KEY`, `JWT_SECRET`
3. **Ne jamais** `git add .env`

## Vérifications

- Le workflow `deploy-production.yml` assemble `.env` via `deploy/build-app-env.sh` **sans logger les valeurs**
- Le fichier `.env` sur le serveur est en mode `600` (propriétaire uniquement)
- L'endpoint `/health` n'expose **aucune** clé API ni URL de base de données

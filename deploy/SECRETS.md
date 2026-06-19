# Secrets GitHub Actions — Agentia OS

**Ne jamais committer de secrets dans le dépôt.** Toutes les valeurs sensibles passent par les **Secrets** du repository GitHub (Settings → Secrets and variables → Actions).

## Secrets obligatoires (production)

**Base de données : PostgreSQL uniquement.** La chaîne de connexion complète (utilisateur, mot de passe, hôte, base) est **exclusivement** dans le secret GitHub — jamais dans le code, jamais commitée, jamais saisie à la main sur le serveur.

| Secret GitHub | Variable `.env` | Description |
|---------------|-----------------|-------------|
| `AGENTIA_OS_DATABASE_URL` | `DATABASE_URL` | **Obligatoire.** URL async PostgreSQL, ex. `postgresql+asyncpg://agentia:MOT_DE_PASSE@127.0.0.1:5432/agentia` |
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

## Serveur de déploiement (Linux ubuntu1)

Production sur le **serveur Linux BedigaCorps** (`ubuntu1` — `51.79.53.197`) :

- Déploiement SSH + systemd + venv Python
- PostgreSQL sur le même serveur (ou accessible depuis celui-ci)
- Secrets **uniquement** dans GitHub Actions → injectés dans `.env` sur le serveur

Guide complet : [deploy/servers/ubuntu1.md](deploy/servers/ubuntu1.md)

## Développement local

1. Copier `.env.example` → `.env` (`.env` est dans `.gitignore`)
2. En local, SQLite est accepté pour dev ; en **production**, seul PostgreSQL via `AGENTIA_OS_DATABASE_URL`
3. Renseigner localement `GEMINI_API_KEY`, `JWT_SECRET` (et `DATABASE_URL` si PostgreSQL local)
4. **Ne jamais** `git add .env` ni mettre la chaîne PostgreSQL dans un fichier du repo

## Vérifications

- `deploy/validate-gha-secrets.sh` — contrôle **tous** les secrets avant deploy (obligatoires + avertissements optionnels)
- Après deploy : healthcheck + test `POST /auth/register` + présence dans OpenAPI
- Le workflow `deploy-production.yml` assemble `.env` via `deploy/build-app-env.sh` **sans logger les valeurs**
- Le fichier `.env` sur le serveur est en mode `600` (propriétaire uniquement)
- L'endpoint `/health` n'expose **aucune** clé API ni URL de base de données

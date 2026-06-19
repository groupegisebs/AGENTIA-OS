# Secrets — Agentia OS

**Modèle identique à [GiseBsPayGateWay](https://github.com/groupegisebs/GiseBsPayGateWay).**

| Guide | Contenu |
|-------|---------|
| **[GITHUB-SECRETS.md](./GITHUB-SECRETS.md)** | 2 secrets GitHub obligatoires (SSH + PostgreSQL) |
| **[SERVER-SECRETS.md](./SERVER-SECRETS.md)** | JWT, Gemini, GiseBsPay → `secrets.json` sur le serveur |

## Résumé rapide

### GitHub Actions (2 secrets)

1. `SSH_PRIVATE_KEY_UBUNTU1` (org) **ou** `AGENTIA_OS_SSH_PRIVATE_KEY`
2. `AGENTIA_OS_CONNECTION_STRING` **ou** `AGENTIA_OS_DATABASE_URL`

Format connection string (comme GiseBsPay) :

```
Host=51.79.53.197;Port=5432;Database=agentia;Username=gisedocuser;Password=...
```

### Serveur Linux (`/opt/apps/agentia-os/secrets.json`)

JWT, Gemini, GiseBsPay — voir [secrets.example.json](./secrets.example.json)

## Vérifications deploy

- Étape **Diagnose secrets** dans GitHub Actions
- `deploy/validate-gha-secrets.sh` avant le SSH
- Post-deploy : `/health` + `POST /auth/register` (≠ 404)

# Secrets GitHub Actions — Agentic Factory (.NET)

Configurer ces secrets dans **Settings → Secrets and variables → Actions** du dépôt.

## Secrets obligatoires

| Nom | Description | Exemple |
|-----|-------------|---------|
| `SSH_PRIVATE_KEY_UBUNTU1` | Clé SSH privée pour accéder à ubuntu1 | Contenu de `~/.ssh/id_ed25519` |
| `AGENTIA_OS_CONNECTION_STRING` | Connection string PostgreSQL complète | `Host=127.0.0.1;Port=5432;Database=agentic_factory;Username=agentia;Password=...` |
| `AGENTIA_OS_JWT_SECRET` | Secret JWT (min 32 caractères) | Chaîne aléatoire longue |

## Secrets optionnels (provider IA)

| Nom | Description |
|-----|-------------|
| `AGENTIA_OS_OPENAI_API_KEY` | Clé API OpenAI |
| `AGENTIA_OS_AZURE_OPENAI_ENDPOINT` | Endpoint Azure OpenAI |
| `AGENTIA_OS_AZURE_OPENAI_API_KEY` | Clé API Azure OpenAI |
| `AGENTIA_OS_AZURE_OPENAI_DEPLOYMENT` | Nom du déploiement Azure |

Sans ces secrets, le système fonctionne en **mode mock** (`AI__Mode=mock`).

## Variables (non sensibles)

Configurer dans **Settings → Secrets and variables → Actions → Variables** :

| Nom | Valeur par défaut | Description |
|-----|-------------------|-------------|
| `SSH_HOST_UBUNTU1` | `51.79.53.197` | IP/hostname du serveur |
| `SSH_USER_UBUNTU1` | `ubuntu` | Utilisateur SSH |
| `SSH_PORT_UBUNTU1` | `22` | Port SSH |
| `AGENTIA_OS_APP_ROOT` | `/opt/apps/agentia-os` | Dossier applicatif |
| `AGENTIA_OS_AI_MODE` | `mock` | `mock`, `openai` ou `azure` |

## Services systemd déployés

| Service | Port | Description |
|---------|------|-------------|
| `agentia-os-api` | `8080` | API Backend |
| `agentia-os-web` | `8081` | Interface Web Admin |
| `agentia-os-runtime` | — | Agent Runtime Worker |

## Commandes de diagnostic serveur

```bash
# Statut des services
sudo systemctl status agentia-os-api agentia-os-web agentia-os-runtime

# Logs en temps réel
sudo journalctl -u agentia-os-api -f
sudo journalctl -u agentia-os-runtime -f

# Health check API
curl http://localhost:8080/health
```

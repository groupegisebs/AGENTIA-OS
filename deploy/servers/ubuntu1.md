# Serveur Linux UBUNTU1 — déploiement Agentia OS

Dépôt : **https://github.com/BedigaCorps/AGENTIA-OS**

Convention BedigaCorps : secrets org **`SSH_*_UBUNTU1`**, secrets repo **`AGENTIA_OS_*`**.

| Serveur | ID | IP |
|---------|-----|-----|
| Ubuntu principal (Linux) | `ubuntu1` | `51.79.53.197` |

Le déploiement GitHub Actions se fait en **SSH** vers ce serveur Linux, puis **systemd** + **venv Python** (même modèle que ComptaDoc-PME, GiseBsPayGateway, etc.).

---

## Architecture sur le serveur

```
/opt/apps/agentia-os/
├── app/          # Code déployé (agent_creator, requirements.txt, alembic)
├── venv/         # Environnement Python 3.11+
├── backups/      # Sauvegardes automatiques avant chaque deploy
└── .env          # Secrets (mode 600, jamais dans git)
```

| Paramètre | Valeur par défaut |
|-----------|-------------------|
| Service systemd | `agentia-os` |
| Port interne | `8000` |
| Utilisateur SSH | `ubuntu` |
| Healthcheck | `http://127.0.0.1:8000/health` |

---

## Secrets organisation BedigaCorps

**https://github.com/organizations/BedigaCorps/settings/secrets/actions**

| Secret org | Valeur |
|------------|--------|
| `SSH_PRIVATE_KEY_UBUNTU1` | Clé privée deploy (multiligne) |
| `SSH_HOST_UBUNTU1` | `51.79.53.197` |
| `SSH_USER_UBUNTU1` | `ubuntu` |
| `SSH_PORT_UBUNTU1` | `22` |

Accès repo : **AGENTIA-OS**.

---

## Secrets dépôt AGENTIA-OS

**https://github.com/BedigaCorps/AGENTIA-OS/settings/secrets/actions**

| Secret | Description |
|--------|-------------|
| `AGENTIA_OS_DATABASE_URL` | **Chaîne PostgreSQL complète** (user, mot de passe, hôte, base). Ex. `postgresql+asyncpg://agentia:****@127.0.0.1:5432/agentia` — **secret GitHub uniquement**, injecté dans `/opt/apps/agentia-os/app/.env` au deploy |
| `AGENTIA_OS_JWT_SECRET` | Secret JWT production |
| `AGENTIA_OS_GEMINI_API_KEY` | Clé Google Gemini |
| `AGENTIA_OS_GISEBS_PAY_API_KEY` | Paiements (optionnel) |
| `AGENTIA_OS_GISEBS_PAY_GATEWAY_URL` | URL GiseBsPayGateway (optionnel) |

### Variables repo (non sensibles)

| Variable | Valeur suggérée |
|----------|-----------------|
| `AGENTIA_OS_APP_ROOT` | `/opt/apps/agentia-os` |
| `AGENTIA_OS_SERVICE_NAME` | `agentia-os` |
| `AGENTIA_OS_LISTEN_PORT` | `8000` |
| `AGENTIA_OS_GISEBS_PAY_SUCCESS_URL` | URL publique `/paiement/succes` |
| `AGENTIA_OS_GISEBS_PAY_CANCEL_URL` | URL publique `/paiement/annule` |

---

## Nginx Proxy Manager (accès public)

Comme les autres apps sur ubuntu1 :

| Champ | Valeur |
|-------|--------|
| Scheme | **`http`** (SSL terminé par NPM) |
| Forward Host | `172.17.0.1` |
| Forward Port | `8000` |

Domaine à configurer selon votre DNS (ex. `agentia.votredomaine.com`).

---

## Préparation du serveur Linux (une fois)

```bash
ssh ubuntu@51.79.53.197

# Répertoire application
sudo mkdir -p /opt/apps/agentia-os
sudo chown ubuntu:ubuntu /opt/apps/agentia-os

# Python 3.11+ (Ubuntu 22.04+)
python3 --version
sudo apt-get update
sudo apt-get install -y python3-venv python3-pip curl

# PostgreSQL — créer base et utilisateur (mot de passe = celui du secret AGENTIA_OS_DATABASE_URL)
sudo -u postgres psql -c "CREATE USER agentia WITH PASSWORD 'VOTRE_MOT_DE_PASSE';"
sudo -u postgres psql -c "CREATE DATABASE agentia OWNER agentia;"
# Puis enregistrer la chaîne complète dans GitHub → Settings → Secrets → AGENTIA_OS_DATABASE_URL
```

Sudo sans mot de passe pour `ubuntu` (systemctl, rsync, chown) — voir modèle ComptaDoc-PME `deploy/README.md`.

---

## Lancer le déploiement

**GitHub → Actions → Deploy Production → Run workflow**

Ou push sur `main` / `master` après merge.

---

## Vérification post-déploiement

```bash
ssh ubuntu@51.79.53.197
sudo systemctl status agentia-os
curl -s http://127.0.0.1:8000/health
journalctl -u agentia-os -n 50 --no-pager
```

Le `.env` de production reste **uniquement** sur le serveur Linux (`/opt/apps/agentia-os/app/.env`, chmod 600).

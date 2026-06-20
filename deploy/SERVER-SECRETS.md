# Secrets serveur — Agentia OS

Les secrets sensibles **ne doivent pas** être dans GitHub. Seuls **SSH** et la **connection string PostgreSQL** passent par GitHub Actions.

**JWT, Gemini et GiseBsPay** vivent dans un fichier **sur le serveur uniquement** — même modèle que GiseBsPayGateWay (`deploy/SERVER-SECRETS.md`).

---

## Où sont stockés les secrets ?

| Secret | Où | GitHub ? |
|--------|-----|----------|
| Clé SSH déploiement | `SSH_PRIVATE_KEY_UBUNTU1` ou `AGENTIA_OS_SSH_PRIVATE_KEY` | Oui |
| Connection string PostgreSQL | `AGENTIA_OS_CONNECTION_STRING` ou `AGENTIA_OS_DATABASE_URL` | Oui → copié dans `app/.env` |
| **JWT** | **`/opt/apps/agentia-os/secrets.json`** | **Non** |
| **Gemini API** | Même fichier | **Non** |
| **GiseBsPay** | Même fichier | **Non** |
| **OAuth** (Google, Facebook, GitHub, Microsoft) | Même fichier | **Non** — voir [OAUTH-SETUP.md](./OAUTH-SETUP.md) |

---

## Étape 1 — Secrets GitHub (déploiement uniquement)

Voir [GITHUB-SECRETS.md](./GITHUB-SECRETS.md) :

1. SSH (org ou dépôt)
2. Connection string avec `Database=agentia`

---

## Étape 2 — Fichier secrets sur le serveur (une fois)

```bash
ssh ubuntu@51.79.53.197
sudo mkdir -p /opt/apps/agentia-os
sudo chown ubuntu:ubuntu /opt/apps/agentia-os
nano /opt/apps/agentia-os/secrets.json
```

Contenu (adapter) — template : [secrets.example.json](./secrets.example.json)

```json
{
  "Jwt": {
    "SecretKey": "votre-cle-jwt-aleatoire-minimum-32-caracteres"
  },
  "Gemini": {
    "ApiKey": "AIza...",
    "Model": "gemini-2.0-flash"
  },
  "GiseBsPay": {
    "GatewayUrl": "https://pay.votredomaine.com",
    "ApiKey": "votre-cle-api",
    "AppCode": "AGENTIAOS"
  }
}
```

Sécuriser :

```bash
chmod 600 /opt/apps/agentia-os/secrets.json
sudo chown ubuntu:ubuntu /opt/apps/agentia-os/secrets.json
sudo systemctl restart agentia-os
```

---

## Comment l'app les charge

1. Au deploy, GitHub injecte dans `/opt/apps/agentia-os/app/.env` :
   - `DATABASE_URL=...`
   - `AGENTIA_SECRETS_FILE=/opt/apps/agentia-os/secrets.json`
2. Au démarrage, FastAPI charge `.env` puis **`secrets.json`** (JWT, Gemini, paiements).

Le deploy **ne remplace jamais** `secrets.json` s'il existe déjà sur le serveur.

---

## Vérification

```bash
ls -la /opt/apps/agentia-os/secrets.json
ls -la /opt/apps/agentia-os/app/.env
sudo systemctl status agentia-os
curl -s http://127.0.0.1:8000/health
```

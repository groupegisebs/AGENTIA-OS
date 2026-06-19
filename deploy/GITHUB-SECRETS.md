# Secrets GitHub — Agentia OS

Le workflow **Deploy Production** échoue tant que **2 secrets** ne sont pas configurés.

**Lien direct (secrets du dépôt)** :  
https://github.com/groupegisebs/AGENTIA-OS/settings/secrets/actions

*(Même modèle que GiseBsPayGateWay — voir `GiseBsPayGateWay/deploy/GITHUB-SECRETS.md`.)*

---

## Créer la base PostgreSQL (SSH, une fois)

```bash
ssh ubuntu@51.79.53.197
sudo -u postgres psql -v ON_ERROR_STOP=1 -c 'CREATE DATABASE agentia OWNER gisedocuser;'
sudo -u postgres psql -d agentia -c 'GRANT ALL ON SCHEMA public TO gisedocuser;'
```

La base est aussi **créée automatiquement** par le workflow si elle n'existe pas.

---

## Étape 1 — Secret SSH (org ou dépôt)

### Option A — Secret organisation (recommandé)

1. GitHub → **Organisation groupegisebs** → **Settings** → **Secrets and variables** → **Actions**
2. Ouvrir `SSH_PRIVATE_KEY_UBUNTU1`
3. **Repository access** → ajouter **AGENTIA-OS**

### Option B — Secret propre au dépôt

1. **Settings** du dépôt → **Secrets and variables** → **Actions** → **New repository secret**
2. Nom : `AGENTIA_OS_SSH_PRIVATE_KEY`
3. Valeur : clé privée `cognidoc_deploy` (multiligne)

---

## Étape 2 — Connection string PostgreSQL (obligatoire, spécifique à ce projet)

Ce secret **doit** être créé pour **AGENTIA-OS** (base `agentia`, pas `gisebs_pay_gateway`).

1. **New repository secret**
2. Nom (au choix, le workflow accepte les deux) :
   - **`AGENTIA_OS_CONNECTION_STRING`** *(format BedigaCorps / .NET)*
   - **`AGENTIA_OS_DATABASE_URL`** *(format SQLAlchemy)*

### Format A — comme GiseBsPay (recommandé)

```
Host=51.79.53.197;Port=5432;Database=agentia;Username=gisedocuser;Password=VOTRE_MOT_DE_PASSE
```

### Format B — SQLAlchemy async

```
postgresql+asyncpg://gisedocuser:VOTRE_MOT_DE_PASSE@127.0.0.1:5432/agentia
```

> **Important** : `Database=agentia` — ne pas copier la chaîne d'un autre projet.

---

## Vérification

1. **Actions** → **Deploy Production** → **Re-run all jobs**
2. L'étape **Diagnose secrets** doit afficher `OK` pour SSH et connection string

| Secret | Statut attendu |
|--------|----------------|
| `SSH_PRIVATE_KEY_UBUNTU1` (org) ou `AGENTIA_OS_SSH_PRIVATE_KEY` | OK |
| `AGENTIA_OS_CONNECTION_STRING` ou `AGENTIA_OS_DATABASE_URL` | OK |

---

## Autres paramètres (défauts workflow)

| Paramètre | Valeur |
|-----------|--------|
| Serveur | `51.79.53.197` |
| User SSH | `ubuntu` |
| App root | `/opt/apps/agentia-os` |
| Service | `agentia-os` |
| Port | `8000` |

> **JWT, Gemini, GiseBsPay** : ne pas les mettre dans GitHub.  
> Voir **[SERVER-SECRETS.md](./SERVER-SECRETS.md)** — fichier `secrets.json` sur le serveur uniquement.

---

## Checklist

- [ ] Accès org `SSH_PRIVATE_KEY_UBUNTU1` **ou** secret `AGENTIA_OS_SSH_PRIVATE_KEY`
- [ ] Secret connection string avec `Database=agentia`
- [ ] Fichier `/opt/apps/agentia-os/secrets.json` sur le serveur (JWT, Gemini)
- [ ] Répertoire `/opt/apps/agentia-os` sur le serveur
- [ ] Re-run du workflow Deploy Production

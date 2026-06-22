# Agentia Factory — Agent Creator (MVP SaaS)

Plateforme SaaS de génération et déploiement de solutions métier. Ce dépôt contient le **MVP Agent Creator** : dialogue en langage naturel, structuration du besoin, **blueprint gratuit**, puis **déploiement facturé à chaque agent**.

## Modèle économique

Agentia Factory est une application **SaaS avec abonnement** : on paie **à chaque déploiement d'agent**.

| Étape | Facturation |
|-------|-------------|
| Dialogue & questions | Inclus dans l'abonnement |
| Génération du blueprint | **Gratuit** |
| Déploiement de l'agent | **Pay-per-deployment** (frais selon le plan + complexité) |

**Devise : EUR** (euros) pour tous les montants affichés et facturés.

### Plans d'abonnement

| Plan | Cible | Déploiements / mois | Frais déploiement de base |
|------|-------|---------------------|---------------------------|
| **Gratuit** | Découverte | 2 | 29 € |
| **Professionnel** | PME | 15 | 49 € |
| **Business** | Multi-départements | 50 | 39 € |
| **Entreprise** | Grandes orgs | Illimité | 29 € |

Le coût final d'un déploiement = frais de base du plan × score de complexité (nombre de composants, type de solution).

## Flux supporté

```
Besoin → Dialogue → Blueprint (gratuit) → Déploiement (facturé) → Agent publié → POST /agents/{id}/invoke
```

| Étape | Endpoint | Notes |
|-------|----------|-------|
| Dialogue | `POST /conversations` | LLM ou mode mock |
| Blueprint | `GET /conversations/{id}/blueprint` | Gratuit |
| Déploiement | `POST /conversations/{id}/deploy` | Paiement GiseBsPay |
| Agent publié | `GET /agents` | Automatique après paiement |
| Invocation | `POST /agents/{id}/invoke` | JWT ou clé API `agt_...` |
| Marketplace | `GET /marketplace/agents` | Agents `public` de toutes les orgs |

Voir [deploy/AGENT-RUNTIME.md](deploy/AGENT-RUNTIME.md) pour la gestion des clés API, des limites et de la visibilité.

## Prérequis

- Python 3.11+
- (Optionnel) Clé API OpenAI ou compatible

## Installation

```bash
cd AGENTIA-OS
python -m venv .venv

# Windows
.venv\Scripts\activate

# Linux / macOS
source .venv/bin/activate

pip install -r requirements.txt
cp .env.example .env   # ou copier manuellement sur Windows
```

Sans `GEMINI_API_KEY` ni `OPENAI_API_KEY`, le service fonctionne en **mode mock** (réponses rule-based). Sans configuration **GiseBsPayGateway**, la facturation utilise un **fournisseur simulé**.

> **Sécurité** : ne jamais committer `.env` ni de clés API. En production, voir [deploy/SECRETS.md](deploy/SECRETS.md).

## Comptes et authentification

| Méthode | Endpoint | Description |
|---------|----------|-------------|
| `POST` | `/auth/register` | Inscription (crée user + org plan Gratuit + JWT) |
| `POST` | `/auth/login` | Connexion |
| `GET` | `/auth/me` | Profil, organisation, plan et quotas |

Toutes les routes `/conversations`, `/organizations` et `/billing` requièrent `Authorization: Bearer <token>`.

## Paiements (GiseBsPayGateway)

Les abonnements et les frais de déploiement passent par [GiseBsPayGateway](https://github.com/BedigaCorps/GiseBsPayGateWay) lorsque les variables `GISEBS_PAY_GATEWAY_URL`, `GISEBS_PAY_APP_CODE` et `GISEBS_PAY_API_KEY` sont définies.

| Flux | Endpoint | Comportement |
|------|----------|--------------|
| Abonnement | `POST /organizations/me/subscribe` | Session checkout (`AGENT-SUB` + plan mensuel) |
| Confirmation abonnement | `POST /organizations/me/subscribe/confirm` | Active le plan après paiement |
| Déploiement | `POST /conversations/{id}/deploy` | Session one-time (`AGENT-DEPLOY` / `DEPLOY-S|M|L`) |
| Confirmation unifiée | `POST /billing/confirm` | Confirme abonnement ou déploiement |
| Polling statut | `GET /billing/payments/{code}/status` | Vérification côté frontend |

**Tiers déploiement GiseBsPay :** `DEPLOY-S` (29 €), `DEPLOY-M` (49 €), `DEPLOY-L` (79 €) selon la complexité.

Configurer les produits/plans correspondants dans l'admin GiseBsPayGateway pour l'application `AGENTIAOS`.

## Persistance

- **Production** : **PostgreSQL** — chaîne de connexion dans le secret GitHub `AGENTIA_OS_DATABASE_URL` uniquement (jamais dans le repo)
- **Développement local** : SQLite par défaut (`.env` gitignored)
- Format production : `postgresql+asyncpg://user:pass@127.0.0.1:5432/agentia`
- Migrations : `alembic upgrade head` (ou `init_db` au démarrage)

## Lancer le serveur

```bash
uvicorn agent_creator.main:app --reload --host 0.0.0.0 --port 8000
```

### Interface web

Une interface web visuelle est intégrée au serveur FastAPI (HTML/CSS/JS vanilla, sans build Node).

| URL | Description |
|-----|-------------|
| http://localhost:8000/ | **Application web** — tableau de bord, chat, blueprint, abonnements, facturation |
| http://localhost:8000/docs | Documentation API interactive (Swagger) |

**Parcours utilisateur :**

- **Inscription / Connexion** — compte + organisation (plan Gratuit)
- **Accueil** — décrire le besoin métier, concevoir la solution
- **Workspace** — dialogue avec l'architecte digital, architecture live, estimations
- **Solution proposée** — résumé exécutif, vue métier, déploiement
- **Mon compte / Abonnements** — plan actuel, upgrade via GiseBsPayGateway
- **Supervision** — solutions déployées et facturation réelle

Documentation API (développeurs) : http://localhost:8000/docs

## API

### Conversations & blueprint

| Méthode | Endpoint | Description |
|---------|----------|-------------|
| `POST` | `/conversations` | Démarre une conversation |
| `GET` | `/conversations` | Liste les conversations |
| `GET` | `/conversations/{id}` | Détail d'une conversation |
| `POST` | `/conversations/{id}/messages` | Continue le dialogue |
| `GET` | `/conversations/{id}/blueprint` | Génère le blueprint (**gratuit**) |
| `POST` | `/conversations/{id}/deploy` | Déploie l'agent (**facturé**) |

### Abonnements & facturation

| Méthode | Endpoint | Description |
|---------|----------|-------------|
| `GET` | `/plans` | Liste les plans d'abonnement |
| `GET` | `/organizations/me` | Organisation courante et usage |
| `GET` | `/organizations/{id}` | Détail d'une organisation |
| `GET` | `/organizations/{id}/billing` | Historique déploiements & facturation |
| `POST` | `/organizations/{id}/subscribe` | Session d'abonnement (GiseBsPayGateway) |
| `GET` | `/organizations/{id}/deployments` | Liste des déploiements |
| `POST` | `/conversations/{id}/deploy/confirm` | Confirme un paiement en attente |
| `GET` | `/health` | Santé du service |

### Exemple complet — conversation → blueprint → déploiement

**1. Démarrer la conversation**

```bash
curl -X POST http://localhost:8000/conversations \
  -H "Content-Type: application/json" \
  -d "{\"message\": \"Chaque fois qu'une facture arrive par email, je veux extraire les données, les enregistrer dans mon système comptable et envoyer un rapport quotidien.\"}"
```

Conserver l'`id` de la conversation.

**2. Enrichir le dialogue (optionnel)**

```bash
curl -X POST http://localhost:8000/conversations/{id}/messages \
  -H "Content-Type: application/json" \
  -d "{\"message\": \"Nous utilisons Sage, environ 50 factures par jour, rapport PDF par email.\"}"
```

**3. Obtenir le blueprint (gratuit)**

```bash
curl http://localhost:8000/conversations/{id}/blueprint
```

La réponse inclut un champ `deployment_hint` avec l'estimation du coût de déploiement.

**4. Déployer l'agent (facturé)**

```bash
curl -X POST http://localhost:8000/conversations/{id}/deploy
```

Réponse : statut du déploiement, montant facturé en EUR, événement de facturation.

**5. Consulter la facturation**

```bash
curl http://localhost:8000/organizations/me
curl http://localhost:8000/organizations/org-demo-0001/billing
```

**6. Voir les plans**

```bash
curl http://localhost:8000/plans
```

## CLI de démonstration

```bash
python -m agent_creator.cli demo
python -m agent_creator.cli chat
python -m agent_creator.cli health
```

## Structure du projet

```
AGENTIA-OS/
├── agent_creator/
│   ├── main.py
│   ├── config.py
│   ├── schemas.py
│   ├── schemas_billing.py
│   ├── cli.py
│   ├── models/
│   │   ├── conversation.py
│   │   ├── requirement.py
│   │   ├── blueprint.py
│   │   ├── subscription.py      # Plans d'abonnement
│   │   ├── organization.py      # Tenant
│   │   ├── deployment.py        # Déploiements facturables
│   │   └── billing.py           # Événements de facturation
│   ├── services/
│   │   ├── store.py
│   │   ├── organization_store.py
│   │   ├── plans.py             # Catalogue des plans
│   │   ├── billing.py           # Calcul coûts & limites
│   │   ├── payment.py           # Mock + GiseBsPayGateway
│   │   ├── gisebs_pay_gateway.py
│   │   ├── deployment.py        # Orchestration déploiement
│   │   ├── llm.py
│   │   ├── extractor.py
│   │   └── blueprint_generator.py
│   ├── static/                  # Interface web (HTML, CSS, JS)
│   │   ├── index.html
│   │   ├── css/styles.css
│   │   └── js/app.js
│   └── routers/
│       ├── conversations.py
│       ├── plans.py
│       └── organizations.py
├── requirements.txt
├── .env.example
└── README.md
```

## Modèles de domaine

- **Conversation** : fil de dialogue utilisateur ↔ agent
- **Requirement** / **Blueprint** : exigences et architecture (génération gratuite)
- **Organization** : tenant avec plan d'abonnement et limites
- **Deployment** : déploiement d'un agent, coût et statut
- **BillingEvent** : enregistrement de chaque charge (déploiement, etc.)

## Configuration

| Variable | Secret GHA | Description |
|----------|------------|-------------|
| `DATABASE_URL` | `AGENTIA_OS_DATABASE_URL` | PostgreSQL production (**obligatoire**) |
| `JWT_SECRET` | `AGENTIA_OS_JWT_SECRET` | Signature JWT (**obligatoire**) |
| `GEMINI_API_KEY` | `AGENTIA_OS_GEMINI_API_KEY` | Clé Google Gemini (**obligatoire prod**) |
| `GEMINI_MODEL` | variable `AGENTIA_OS_GEMINI_MODEL` | Modèle Gemini (défaut `gemini-2.0-flash`) |
| `LLM_PROVIDER` | variable `AGENTIA_OS_LLM_PROVIDER` | `gemini`, `openai`, `auto`, `mock` |
| `GISEBS_PAY_API_KEY` | `AGENTIA_OS_GISEBS_PAY_API_KEY` | Paiements GiseBsPayGateway |

Liste complète : [deploy/SECRETS.md](deploy/SECRETS.md)

## Déploiement (GitHub Actions → serveur Linux ubuntu1)

Production sur **ubuntu1** (`51.79.53.197`) — même infrastructure que ComptaDoc-PME et GiseBsPayGateway.

| Élément | Valeur |
|---------|--------|
| Serveur | Linux Ubuntu (`ubuntu@51.79.53.197`) |
| Chemin app | `/opt/apps/agentia-os` |
| Service | `agentia-os` (systemd) |
| Port | `8000` |

Guide détaillé : [deploy/servers/ubuntu1.md](deploy/servers/ubuntu1.md)

### CI (`ci.yml`)

- Déclenché sur push/PR vers `main` / `master`
- `ruff check`, vérification d'import, `pytest`

### Production (`deploy-production.yml`)

- Tests puis déploiement SSH (modèle **ComptaDoc-PME** / **GiseMailSender**)
- Bundle Python (`agent_creator/` + `requirements.txt`) installé dans un venv sur le serveur
- Healthcheck : `GET /health`

### Secrets GitHub requis

| Secret | Description |
|--------|-------------|
| `AGENTIA_OS_DATABASE_URL` | URL PostgreSQL (`postgresql+asyncpg://...`) — **jamais dans le repo** |
| `AGENTIA_OS_JWT_SECRET` | Secret JWT (chaîne aléatoire longue) |
| `AGENTIA_OS_GEMINI_API_KEY` | Clé API Google Gemini — **jamais dans le repo** |
| `AGENTIA_OS_SSH_PRIVATE_KEY` ou `SSH_PRIVATE_KEY_UBUNTU1` | Clé SSH de déploiement |
| `AGENTIA_OS_GISEBS_PAY_API_KEY` | Clé GiseBsPayGateway (optionnel) |
| `AGENTIA_OS_GISEBS_PAY_GATEWAY_URL` | URL gateway paiement (optionnel) |

Le workflow assemble `.env` via `deploy/build-app-env.sh` **sans afficher les valeurs** dans les logs.

Variables non sensibles (GitHub **Variables**) : `SSH_HOST_UBUNTU1`, `AGENTIA_OS_APP_ROOT`, `AGENTIA_OS_GEMINI_MODEL`, etc.

Voir [deploy/SECRETS.md](deploy/SECRETS.md) pour la procédure complète.

### Docker (optionnel)

```bash
docker build -t agentia-os .
docker run --rm -p 8000:8000 --env-file .env agentia-os
```

## Pistes d'évolution (hors MVP)

- Persistance PostgreSQL / Redis
- Authentification multi-tenant réelle
- Webhooks GiseBsPayGateway pour activation automatique des abonnements
- Export blueprint vers moteur de déploiement

## Licence

Projet interne AGENTIA-OS — MVP de démonstration.

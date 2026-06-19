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
Besoin → Dialogue → Blueprint (gratuit) → Déploiement (facturé) → Agent en production
```

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

Sans `OPENAI_API_KEY`, le service fonctionne en **mode mock** (réponses rule-based, démo hors-ligne). Sans configuration **GiseBsPayGateway**, la facturation utilise un **fournisseur simulé** (aucune clé API requise).

## Paiements (GiseBsPayGateway)

Les abonnements et les frais de déploiement passent par [GiseBsPayGateway](https://github.com/BedigaCorps/GiseBsPayGateWay) lorsque les variables `GISEBS_PAY_GATEWAY_URL`, `GISEBS_PAY_APP_CODE` et `GISEBS_PAY_API_KEY` sont définies.

| Flux | Endpoint | Comportement |
|------|----------|--------------|
| Abonnement | `POST /organizations/{id}/subscribe` | Crée une session checkout (`AGENT-SUB` + plan mensuel) |
| Déploiement | `POST /conversations/{id}/deploy` | Crée une session one-time (`AGENT-DEPLOY` / `ONE-TIME`) |
| Confirmation | `POST /conversations/{id}/deploy/confirm` | Vérifie le paiement et finalise le déploiement |

Configurer les produits/plans correspondants dans l'admin GiseBsPayGateway pour l'application `AGENTIAOS`.

## Lancer le serveur

```bash
uvicorn agent_creator.main:app --reload --host 0.0.0.0 --port 8000
```

Documentation interactive : http://localhost:8000/docs

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

| Variable | Description | Défaut |
|----------|-------------|--------|
| `OPENAI_API_KEY` | Clé API | *(vide = mock)* |
| `DEFAULT_ORGANIZATION_ID` | Tenant MVP | `org-demo-0001` |
| `DEPLOYMENT_BASE_FEE` | Override frais de base (0 = plan) | `0` |
| `DEPLOYMENT_COMPLEXITY_MULTIPLIER` | Majoration par composant | `0.15` |
| `BILLING_CURRENCY` | Devise | `EUR` |
| `GISEBS_PAY_GATEWAY_URL` | URL GiseBsPayGateway | *(vide = mock)* |
| `GISEBS_PAY_APP_CODE` | Code application cliente | `AGENTIAOS` |
| `GISEBS_PAY_API_KEY` | Clé API application | *(vide = mock)* |

## Déploiement (GitHub Actions)

Le dépôt suit le même modèle que les autres projets BedigaCorps (SSH vers `ubuntu1`, service systemd).

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
| `AGENTIA_OS_SSH_PRIVATE_KEY` ou `SSH_PRIVATE_KEY_UBUNTU1` | Clé SSH de déploiement |
| `SSH_HOST_UBUNTU1` (variable ou secret) | Hôte (défaut `51.79.53.197`) |
| `SSH_USER_UBUNTU1` | Utilisateur SSH (défaut `ubuntu`) |
| `AGENTIA_OS_APP_ROOT` | Répertoire (défaut `/opt/apps/agentia-os`) |
| `AGENTIA_OS_SERVICE_NAME` | Service systemd (défaut `agentia-os`) |
| `AGENTIA_OS_LISTEN_PORT` | Port (défaut `8000`) |
| `AGENTIA_OS_APP_ENV` | Contenu multiligne du fichier `.env` de production |

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

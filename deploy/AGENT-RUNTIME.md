# Guide — Agent Runtime

Agentia OS publie automatiquement un agent exécutable dès qu'un déploiement passe au statut `deployed` (paiement confirmé). Ce guide couvre la gestion des agents publiés, des clés API et des quotas.

## Flux de publication

```
POST /conversations/{id}/deploy
  → paiement GiseBsPay
  → DeploymentStatus.DEPLOYED
  → AgentPublish.publish_from_deployment()   ← automatique
  → GET /agents  affiche le nouvel agent
```

La publication est **idempotente** : relancer le déploiement sur une conversation déjà déployée retourne l'agent existant.

---

## Invocation

### Avec JWT (utilisateur connecté)

```bash
curl -X POST https://votre-domaine/agents/{agent_id}/invoke \
  -H "Authorization: Bearer <token>" \
  -H "Content-Type: application/json" \
  -d '{"message": "Bonjour, quel est ton rôle ?"}'
```

### Avec clé API agent

Créer une clé depuis le cockpit ou via API :

```bash
curl -X POST https://votre-domaine/agents/{agent_id}/api-keys \
  -H "Authorization: Bearer <token>" \
  -H "Content-Type: application/json" \
  -d '{"label": "ma-clé-prod"}'
```

La clé (`agt_...`) n'est affichée **qu'une seule fois**. Invoquer ensuite :

```bash
curl -X POST https://votre-domaine/agents/{agent_id}/invoke \
  -H "X-Agent-Key: agt_xxxx..." \
  -H "Content-Type: application/json" \
  -d '{"message": "Traite cette facture"}'
```

---

## Visibilité

| Valeur | Accès |
|--------|-------|
| `private` | Seulement les membres de l'organisation créatrice (défaut) |
| `organization` | Idem (réservé usage futur multi-équipe) |
| `public` | Visible de toutes les organisations dans `GET /marketplace/agents` |

Changer la visibilité :

```bash
curl -X PATCH https://votre-domaine/agents/{agent_id} \
  -H "Authorization: Bearer <token>" \
  -H "Content-Type: application/json" \
  -d '{"visibility": "public"}'
```

---

## Limites (quotas)

Les limites sont définies par le plan d'abonnement et encodées dans le manifest de l'agent :

| Plan | max_input_chars | max_requests_per_hour |
|------|-----------------|-----------------------|
| Gratuit | 2 000 | 20 |
| Professionnel | 4 000 | 60 |
| Business | 8 000 | 200 |
| Entreprise | 16 000 | 1 000 |

Dépassement → HTTP `429 Too Many Requests`.  
Message trop long → HTTP `422 Unprocessable Entity`.

---

## Sécurité

- Isolation tenant : un agent `private` ne peut être invoqué que par son organisation.
- Rate limit compté par `(agent_id, organization_id)` sur une fenêtre glissante d'une heure.
- Clés API stockées en SHA-256 (jamais en clair). Révoquer via `DELETE /agents/{id}/api-keys/{key_id}`.
- PII filter actif par défaut dans le manifest (`pii_filter: true`) — à connecter à un middleware de filtrage en production.

---

## Endpoints de référence

| Méthode | Route | Auth | Description |
|---------|-------|------|-------------|
| `GET` | `/agents` | JWT | Agents de l'organisation |
| `GET` | `/agents/{id}` | JWT | Détail + manifest |
| `PATCH` | `/agents/{id}` | JWT owner | Visibilité / description / statut |
| `POST` | `/agents/{id}/invoke` | JWT ou clé | Invocation |
| `POST` | `/agents/{id}/api-keys` | JWT owner | Créer clé API |
| `GET` | `/agents/{id}/api-keys` | JWT owner | Lister les clés |
| `DELETE` | `/agents/{id}/api-keys/{key_id}` | JWT owner | Révoquer une clé |
| `GET` | `/marketplace/agents` | JWT | Agents publics (toutes orgs) |

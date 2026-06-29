# Catalogue Pay Gateway — application AGENTIAOS

L'application cliente **AGENTIAOS** doit exister dans [GISEBS Pay Gateway](https://gisebsapipaygateway.gisebs.com/) avant tout checkout.

Le checkout AGENTIA-OS échoue si le **AppCode**, la **clé API** ou le **produit/plan** catalogue n'existent pas côté Pay Gateway.

## 1. Créer l'application cliente (obligatoire)

1. Admin Pay Gateway → **Applications** → **Nouvelle application**
2. **AppCode** : `AGENTIAOS` (identique à `GisebsApiPayGateway:AppCode`)
3. **Nom** : AGENTIA-OS (ou libellé interne)
4. **Domaines autorisés** (optionnel) : URL publique de l'app web (ex. `agentia.gisebs.com`)
5. **Générer une clé API** → copier la valeur `gbsk_...` dans `GisebsApiPayGateway:ApiKey` (Development.json / secrets serveur uniquement)

### Diagnostic 401 « Application cliente invalide »

| Message Pay Gateway | Cause | Action |
|---------------------|-------|--------|
| `Application cliente invalide.` | AppCode absent ou inactif | Créer/activer AGENTIAOS dans l'admin |
| `API Key invalide.` | AppCode OK, clé incorrecte | Régénérer la clé pour AGENTIAOS |
| `AppCode et API Key requis.` | En-têtes manquants | Vérifier le client HTTP (X-App-Code, X-Api-Key) |

Test rapide :

```powershell
curl.exe -s -H "X-App-Code: AGENTIAOS" -H "X-Api-Key: gbsk_VOTRE_CLE" `
  "https://gisebsapipaygateway.gisebs.com/api/products"
```

Réponse attendue : `[]` ou liste JSON — **pas** `{"error":"Application cliente invalide."}`.

## 2. Produits d'abonnement

AGENTIA-OS mappe chaque plan interne vers un **ProductCode** Pay Gateway :

| Plan AGENTIA-OS | ProductCode Pay Gateway | PlanCode | Montant (USD) |
|-----------------|-------------------------|----------|---------------|
| Starter | `AGENTIA-STARTER` | `MONTHLY` | 99 |
| Pro | `AGENTIA-PRO` | `MONTHLY` | 299 |
| Enterprise | `AGENTIA-ENTERPRISE` | `MONTHLY` | 999 |

Le préfixe est configurable via `GisebsApiPayGateway:ProductCodePrefix` (défaut `AGENTIA`).

### Création manuelle (admin)

1. **Produits** → filtre **AGENTIAOS** → **Nouveau produit**
2. **ProductCode** exact (ex. `AGENTIA-PRO`)
3. **Plans** → plan `MONTHLY`, montant et devise USD
4. **Sync Stripe** si les identifiants Stripe ne sont pas renseignés

### Synchronisation automatique (AGENTIA-OS)

Au démarrage de l'API (`IdentitySeedService`), si Pay Gateway est configuré :

1. Pour chaque plan avec `MonthlyPriceUsd > 0`, appel `POST api/products/catalog`
2. Ignore les entrées déjà existantes
3. Journalise un avertissement si AGENTIAOS n'est pas encore enregistré (401)

Script manuel : `deploy/sync-paygateway-catalog.ps1`

## 3. Stripe

Menu **Stripe** dans Pay Gateway : clés publishable + secret + webhook secret.

Webhook Stripe → `https://gisebsapipaygateway.gisebs.com/api/webhooks/stripe`

Événements recommandés : `checkout.session.completed`, `payment_intent.succeeded`, `invoice.paid`, `customer.subscription.updated`, `customer.subscription.deleted`.

> Pay Gateway **ne notifie pas** les applications clientes. AGENTIA-OS confirme le paiement via `GET api/payments/{paymentCode}` à la page Success (avec tentatives automatiques).

## 4. Configuration AGENTIA-OS

`appsettings.json` (sans clé) :

```json
"GisebsApiPayGateway": {
  "BaseUrl": "https://gisebsapipaygateway.gisebs.com",
  "AppCode": "AGENTIAOS",
  "ApiKey": "",
  "WebhookSecret": "",
  "DefaultPlanCode": "MONTHLY",
  "ProductCodePrefix": "AGENTIA",
  "RequireHttps": true
}
```

`appsettings.Development.json` : renseigner `ApiKey` (et `WebhookSecret` si webhook interne utilisé).

## 5. Flux checkout

1. Web → `POST /api/billing/checkout` avec `successUrl` / `cancelUrl`
2. API → `POST /api/checkout/session` (Pay Gateway) avec en-têtes `X-App-Code`, `X-Api-Key`
3. Redirection Stripe Checkout
4. Success → `POST /api/billing/payments/confirm` → poll `GET /api/payments/{code}` (jusqu'à 5 tentatives)
5. Activation abonnement organisation

URLs de retour par défaut (Web) :

- Succès : `{PublicWebBaseUrl}/Subscriptions/Success?checkoutId={id}`
- Annulation : `{PublicWebBaseUrl}/Subscriptions/Cancel`

## 6. Webhook interne AGENTIA-OS (optionnel)

`POST /api/billing/webhook` avec en-tête `X-Webhook-Secret` — pour automation externe, pas pour Stripe directement.

# OAuth — Google, Facebook, GitHub, Microsoft

## Configuration (`secrets.json` serveur)

Copiez la section `OAuth` depuis `deploy/secrets.example.json` vers `/opt/apps/agentia-os/secrets.json`.

| Clé | Description |
|-----|-------------|
| `RedirectBaseUrl` | URL publique du site (sans slash final), ex. `https://app.agentia.com` |
| `Google.ClientId` / `ClientSecret` | [Google Cloud Console](https://console.cloud.google.com/) → APIs & Services → Credentials |
| `Facebook.AppId` / `AppSecret` | [Meta for Developers](https://developers.facebook.com/) |
| `GitHub.ClientId` / `ClientSecret` | GitHub → Settings → Developer settings → OAuth Apps |
| `Microsoft.ClientId` / `ClientSecret` / `TenantId` | [Azure Portal](https://portal.azure.com/) → App registrations (`TenantId`: `common` ou votre tenant) |

## Redirect URI (identique pour chaque provider)

Enregistrez ces URLs exactes chez chaque fournisseur :

```
{RedirectBaseUrl}/auth/oauth/google/callback
{RedirectBaseUrl}/auth/oauth/facebook/callback
{RedirectBaseUrl}/auth/oauth/github/callback
{RedirectBaseUrl}/auth/oauth/microsoft/callback
```

Exemple local : `http://localhost:8000/auth/oauth/google/callback`

## Développement local (`.env`)

```env
OAUTH_REDIRECT_BASE_URL=http://localhost:8000
OAUTH_GOOGLE_CLIENT_ID=...
OAUTH_GOOGLE_CLIENT_SECRET=...
```

## Flux

1. L'utilisateur clique sur Google / Facebook / GitHub / Microsoft
2. Redirection vers `/auth/oauth/{provider}` puis le fournisseur
3. Callback → création ou liaison du compte → JWT
4. Redirection SPA `/connexion/oauth#token=...`

## API

- `GET /auth/oauth/providers` — liste des providers configurés
- `GET /auth/oauth/{provider}` — démarre le flux
- `GET /auth/oauth/{provider}/callback` — callback (utilisé par le provider)

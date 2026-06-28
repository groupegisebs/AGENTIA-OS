# Infrastructure — ubuntu1

| Élément | Valeur |
|---------|--------|
| Serveur | Ubuntu (`ubuntu@51.79.53.197`) |
| Chemin app | `/opt/apps/agentia-os` |
| Services systemd | `agentia-os-api`, `agentia-os-web`, `agentia-os-runtime` |
| Ports | API: `8080`, Web: `8081` |

## Préparation initiale (une seule fois)

```bash
# Depuis la machine locale
scp deploy/setup-server.sh ubuntu@51.79.53.197:/tmp/
scp deploy/agentia-os-*.service ubuntu@51.79.53.197:/tmp/

# Sur le serveur
ssh ubuntu@51.79.53.197
sudo bash /tmp/setup-server.sh
```

## Structure sur le serveur

```
/opt/apps/agentia-os/
├── api/          ← bundle dotnet publish AgenticFactory.Api
├── web/          ← bundle dotnet publish AgenticFactory.Web
├── runtime/      ← bundle dotnet publish AgenticFactory.Runtime.WindowsService
├── .env.api      ← écrit automatiquement par CI/CD
├── .env.web      ← écrit automatiquement par CI/CD
└── .env.runtime  ← écrit automatiquement par CI/CD
```

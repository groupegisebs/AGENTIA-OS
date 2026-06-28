#!/usr/bin/env bash
# =============================================================================
# setup-server.sh — Préparation serveur Ubuntu pour Agentic Factory (.NET)
# Exécuter UNE SEULE FOIS en tant que root ou avec sudo sur ubuntu1
# =============================================================================
set -euo pipefail

APP_ROOT="/opt/apps/agentia-os"
APP_USER="agentia"
DOTNET_VERSION="10.0"

echo "=== 1. Installation .NET ${DOTNET_VERSION} ==="
# Microsoft package feed
wget -q https://packages.microsoft.com/config/ubuntu/$(lsb_release -rs)/packages-microsoft-prod.deb -O /tmp/ms.deb
dpkg -i /tmp/ms.deb
rm /tmp/ms.deb
apt-get update -q
apt-get install -y --no-install-recommends dotnet-runtime-${DOTNET_VERSION} aspnetcore-runtime-${DOTNET_VERSION}
dotnet --list-runtimes

echo "=== 2. Création utilisateur système ==="
id -u "${APP_USER}" &>/dev/null || useradd --system --no-create-home --shell /usr/sbin/nologin "${APP_USER}"

echo "=== 3. Création arborescence applicative ==="
mkdir -p "${APP_ROOT}/api"
mkdir -p "${APP_ROOT}/web"
mkdir -p "${APP_ROOT}/runtime"
mkdir -p "${APP_ROOT}/logs"
chown -R "${APP_USER}:${APP_USER}" "${APP_ROOT}"
chmod 750 "${APP_ROOT}"

echo "=== 4. Installation des services systemd ==="
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cp "${SCRIPT_DIR}/agentia-os-api.service"     /etc/systemd/system/
cp "${SCRIPT_DIR}/agentia-os-web.service"     /etc/systemd/system/
cp "${SCRIPT_DIR}/agentia-os-runtime.service" /etc/systemd/system/

systemctl daemon-reload
systemctl enable agentia-os-api agentia-os-web agentia-os-runtime

echo ""
echo "=== Serveur prêt ==="
echo "Prochaines étapes :"
echo "  1. Créer les fichiers .env.api / .env.web / .env.runtime dans ${APP_ROOT}/"
echo "     (le workflow CI/CD le fait automatiquement)"
echo "  2. Déposer les bundles publiés dans api/ web/ runtime/"
echo "  3. sudo systemctl start agentia-os-api agentia-os-web agentia-os-runtime"
echo "  4. Configurer Nginx en reverse-proxy sur les ports 8080/8081"
echo ""
echo "Voir deploy/GITHUB-SECRETS.md pour la configuration des secrets GitHub Actions."

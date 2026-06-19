#!/usr/bin/env bash
# Vérifie les 2 secrets GitHub obligatoires (modèle GiseBsPayGateway).

set -euo pipefail

MISSING=0

echo "Dépôt : ${GITHUB_REPOSITORY:-local}"
echo ""

echo "Secrets SSH (un seul suffit) :"
if [ -n "${AGENTIA_OS_SSH_PRIVATE_KEY:-}" ]; then
  echo "  AGENTIA_OS_SSH_PRIVATE_KEY  → OK"
elif [ -n "${SSH_PRIVATE_KEY_UBUNTU1:-}" ]; then
  echo "  SSH_PRIVATE_KEY_UBUNTU1     → OK (org)"
else
  echo "  SSH_PRIVATE_KEY_UBUNTU1     → manquant"
  echo "  AGENTIA_OS_SSH_PRIVATE_KEY  → manquant"
  echo "::error::Clé SSH manquante — voir deploy/GITHUB-SECRETS.md"
  MISSING=1
fi

echo ""
echo "Connection string (un seul suffit) :"
if [ -n "${AGENTIA_OS_DATABASE_URL:-}" ]; then
  echo "  AGENTIA_OS_DATABASE_URL       → OK"
elif [ -n "${AGENTIA_OS_CONNECTION_STRING:-}" ]; then
  echo "  AGENTIA_OS_CONNECTION_STRING  → OK"
elif [ -n "${CONNECTION_STRING:-}" ]; then
  echo "  CONNECTION_STRING (workflow)  → OK"
else
  echo "  AGENTIA_OS_CONNECTION_STRING  → manquant"
  echo "  AGENTIA_OS_DATABASE_URL       → manquant"
  echo "::error::Connection string PostgreSQL manquante — Database=agentia"
  MISSING=1
fi

echo ""
echo "Secrets serveur (non GitHub — optionnel au deploy) :"
echo "  secrets.json sur ubuntu1      → JWT, Gemini, GiseBsPay (deploy/SERVER-SECRETS.md)"

if [ -n "${AGENTIA_OS_JWT_SECRET:-}" ] || [ -n "${AGENTIA_OS_GEMINI_API_KEY:-}" ]; then
  echo "::warning::JWT/Gemini détectés dans GitHub — préférez secrets.json sur le serveur"
fi

echo ""
echo "Cible : ${SSH_USER:-ubuntu}@${SSH_HOST:-51.79.53.197}:${SSH_PORT:-22}"
echo "Guide : deploy/GITHUB-SECRETS.md"

[ "$MISSING" -eq 0 ] || exit 1
echo "Validation secrets GitHub OK"

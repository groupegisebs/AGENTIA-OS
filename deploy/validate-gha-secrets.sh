#!/usr/bin/env bash
# Vérifie la présence des secrets GitHub Actions avant déploiement.
# N'affiche jamais les valeurs — uniquement présent / absent / format.

set -euo pipefail

MISSING=0

ok() {
  echo "OK   $1"
}

fail() {
  echo "::error::$1"
  MISSING=1
}

warn() {
  echo "::warning::$1"
}

# --- SSH (obligatoire : l'un des deux) ---
if [ -n "${AGENTIA_OS_SSH_PRIVATE_KEY:-}" ]; then
  ok "AGENTIA_OS_SSH_PRIVATE_KEY"
elif [ -n "${SSH_PRIVATE_KEY_UBUNTU1:-}" ]; then
  ok "SSH_PRIVATE_KEY_UBUNTU1 (org)"
else
  fail "Clé SSH manquante — définir AGENTIA_OS_SSH_PRIVATE_KEY ou SSH_PRIVATE_KEY_UBUNTU1"
fi

# --- PostgreSQL (obligatoire) ---
if [ -z "${DATABASE_URL:-}" ]; then
  fail "Secret AGENTIA_OS_DATABASE_URL manquant (DATABASE_URL)"
else
  case "${DATABASE_URL}" in
    postgresql+asyncpg://*|postgresql://*|postgres://*)
      ok "AGENTIA_OS_DATABASE_URL (PostgreSQL)"
      ;;
    *)
      fail "AGENTIA_OS_DATABASE_URL doit être une URL PostgreSQL (postgresql+asyncpg://...)"
      ;;
  esac
fi

# --- JWT (obligatoire) ---
if [ -z "${JWT_SECRET:-}" ]; then
  fail "Secret AGENTIA_OS_JWT_SECRET manquant"
elif [ "${#JWT_SECRET}" -lt 32 ]; then
  fail "AGENTIA_OS_JWT_SECRET trop court (minimum 32 caractères)"
else
  ok "AGENTIA_OS_JWT_SECRET"
fi

# --- LLM (recommandé) ---
if [ -z "${GEMINI_API_KEY:-}" ] && [ -z "${OPENAI_API_KEY:-}" ]; then
  warn "Aucune clé LLM — AGENTIA_OS_GEMINI_API_KEY ou OPENAI_API_KEY (mode mock en production)"
else
  [ -n "${GEMINI_API_KEY:-}" ] && ok "AGENTIA_OS_GEMINI_API_KEY"
  [ -n "${OPENAI_API_KEY:-}" ] && ok "OPENAI_API_KEY"
fi

# --- Paiements (recommandé) ---
if [ -z "${GISEBS_PAY_API_KEY:-}" ]; then
  warn "AGENTIA_OS_GISEBS_PAY_API_KEY absent — paiements simulés"
else
  ok "AGENTIA_OS_GISEBS_PAY_API_KEY"
fi

if [ -z "${GISEBS_PAY_GATEWAY_URL:-}" ]; then
  warn "AGENTIA_OS_GISEBS_PAY_GATEWAY_URL absent — paiements simulés"
else
  ok "AGENTIA_OS_GISEBS_PAY_GATEWAY_URL"
fi

# --- Cible SSH (info, défauts workflow) ---
HOST="${SSH_HOST:-51.79.53.197}"
USER="${SSH_USER:-ubuntu}"
PORT="${SSH_PORT:-22}"
echo "Cible deploy : ${USER}@${HOST}:${PORT}"

if [ "$MISSING" -ne 0 ]; then
  echo ""
  echo "Secrets obligatoires manquants — voir deploy/SECRETS.md"
  exit 1
fi

echo "Validation secrets OK"

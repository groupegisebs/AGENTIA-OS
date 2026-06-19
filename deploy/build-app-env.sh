#!/usr/bin/env bash
# Assemble le fichier .env de production à partir de variables d'environnement (secrets GHA).
# Ne JAMAIS afficher les valeurs — uniquement les noms des variables écrites.

set -euo pipefail

OUT="${1:-/tmp/agentia-os.app.env}"
umask 077
: > "$OUT"

write_if_set() {
  local name="$1"
  local value="${!name:-}"
  if [ -n "$value" ]; then
    # Valeur sur une ligne ; pas d'expansion shell dans le secret
    printf '%s=%s\n' "$name" "$value" >> "$OUT"
    echo "$name"
  fi
}

WRITTEN=()

append() {
  local name="$1"
  if write_if_set "$name" > /dev/null; then
    WRITTEN+=("$name")
  fi
}

# --- Secrets obligatoires en production ---
append DATABASE_URL
append JWT_SECRET
append GEMINI_API_KEY

# --- LLM (Gemini prioritaire si clé présente) ---
append LLM_PROVIDER
append GEMINI_MODEL
append OPENAI_API_KEY
append OPENAI_BASE_URL
append OPENAI_MODEL

# --- GiseBsPayGateway ---
append GISEBS_PAY_GATEWAY_URL
append GISEBS_PAY_APP_CODE
append GISEBS_PAY_API_KEY
append GISEBS_PAY_SUCCESS_URL
append GISEBS_PAY_CANCEL_URL

# --- Non-secrets (peuvent venir de vars GHA) ---
append HOST
append PORT
append BILLING_CURRENCY

chmod 600 "$OUT"
echo "Fichier .env généré : ${OUT} (${#WRITTEN[@]} variable(s) : ${WRITTEN[*]})"

if [ -z "${DATABASE_URL:-}" ]; then
  echo "::error::DATABASE_URL manquant — définir le secret AGENTIA_OS_DATABASE_URL"
  exit 1
fi
if [ -z "${JWT_SECRET:-}" ]; then
  echo "::error::JWT_SECRET manquant — définir le secret AGENTIA_OS_JWT_SECRET"
  exit 1
fi
if [ -z "${GEMINI_API_KEY:-}" ] && [ -z "${OPENAI_API_KEY:-}" ]; then
  echo "::warning::Aucune clé LLM (GEMINI_API_KEY / OPENAI_API_KEY) — mode mock en production"
fi

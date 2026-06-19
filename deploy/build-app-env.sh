#!/usr/bin/env bash
# Assemble le .env minimal de production (modèle GiseBsPayGateway).
# GitHub : SSH + connection string uniquement. JWT/Gemini → secrets.json serveur.

set -euo pipefail

OUT="${1:-/tmp/agentia-os.app.env}"
APP_ROOT="${APP_ROOT:-/opt/apps/agentia-os}"
umask 077
: > "$OUT"

normalize_db_url() {
  local raw="$1"
  python3 "$(dirname "$0")/normalize_db_url.py" "$raw"
}

if [ -z "${CONNECTION_STRING:-}" ] && [ -z "${DATABASE_URL:-}" ]; then
  echo "::error::Connection string manquante — secret AGENTIA_OS_CONNECTION_STRING ou AGENTIA_OS_DATABASE_URL"
  exit 1
fi

RAW_DB="${DATABASE_URL:-${CONNECTION_STRING:-}}"
DATABASE_URL="$(normalize_db_url "$RAW_DB")"

case "${DATABASE_URL}" in
  postgresql+asyncpg://*) ;;
  *)
    echo "::error::URL PostgreSQL invalide après conversion"
    exit 1
    ;;
esac

{
  printf 'DATABASE_URL=%s\n' "$DATABASE_URL"
  printf 'AGENTIA_SECRETS_FILE=%s/secrets.json\n' "$APP_ROOT"
  printf 'HOST=0.0.0.0\n'
  printf 'PORT=%s\n' "${LISTEN_PORT:-8000}"
} >> "$OUT"

chmod 600 "$OUT"
echo "Fichier .env minimal généré : ${OUT} (DATABASE_URL + AGENTIA_SECRETS_FILE)"
echo "JWT/Gemini/GiseBsPay → voir deploy/SERVER-SECRETS.md (secrets.json sur le serveur)"

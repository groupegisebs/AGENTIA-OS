#!/usr/bin/env bash
# Vérifie qu'aucun secret n'est présent dans les fichiers versionnés.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

PATTERNS=(
  'AIza[0-9A-Za-z_-]{20,}'
  'sk-[a-zA-Z0-9]{20,}'
  'postgresql([+]asyncpg)?://[^@]+:[^@]+@'
  'Bearer [a-zA-Z0-9._-]{20,}'
)

EXCLUDES=(
  '--glob=!.env.example'
  '--glob=!deploy/SECRETS.md'
  '--glob=!*.md'
  '--glob=!.github/**'
)

FOUND=0
for pat in "${PATTERNS[@]}"; do
  if rg -n "${EXCLUDES[@]}" "$pat" . 2>/dev/null; then
    FOUND=1
  fi
done

if [ -f .env ]; then
  echo "::error::Fichier .env présent — ne doit pas être commité"
  FOUND=1
fi

if [ "$FOUND" -ne 0 ]; then
  echo "::error::Secrets potentiels détectés dans le dépôt"
  exit 1
fi

echo "Aucun secret détecté dans les fichiers versionnés."

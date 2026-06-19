#!/usr/bin/env bash
# Déploiement Agentia OS (FastAPI) depuis GitHub Actions vers Ubuntu (systemd).

set -euo pipefail

: "${SSH_HOST:?SSH_HOST requis}"
: "${SSH_USER:?SSH_USER requis}"
: "${APP_ROOT:?APP_ROOT requis}"
: "${SERVICE_NAME:?SERVICE_NAME requis}"
: "${LISTEN_PORT:=8000}"
: "${CONNECTION_STRING:?CONNECTION_STRING requis}"
: "${PUBLISH_DIR:=publish}"
: "${SSH_PORT:=22}"
: "${APP_NAME:=Agentia Factory — Agent Creator}"

sanitize() {
  printf '%s' "$1" | tr -d '\r\n\t' | sed -e 's/^[[:space:]]*//' -e 's/[[:space:]]*$//' -e 's/^"//' -e 's/"$//' -e "s/^'//" -e "s/'$//"
}

SSH_HOST=$(sanitize "${SSH_HOST}")
SSH_HOST="${SSH_HOST#http://}"
SSH_HOST="${SSH_HOST#https://}"
SSH_HOST="${SSH_HOST%%/*}"
SSH_USER=$(sanitize "${SSH_USER}")
SSH_PORT=$(sanitize "${SSH_PORT}")
APP_ROOT=$(sanitize "${APP_ROOT}")
SERVICE_NAME=$(sanitize "${SERVICE_NAME}")
LISTEN_PORT=$(sanitize "${LISTEN_PORT}")
CONNECTION_STRING=$(sanitize "${CONNECTION_STRING}")

APP_DIR="${APP_ROOT}/app"
BACKUP_DIR="${APP_ROOT}/backups"
VENV_DIR="${APP_ROOT}/venv"
STAGING_REMOTE="/tmp/${SERVICE_NAME}-gha-$(date +%Y%m%d-%H%M%S)"
SSH_OPTS=(-p "${SSH_PORT}" -o BatchMode=yes -o StrictHostKeyChecking=yes)
SCP_OPTS=(-P "${SSH_PORT}" -o BatchMode=yes -o StrictHostKeyChecking=yes)
SSH_TARGET="${SSH_USER}@${SSH_HOST}"

if [[ -n "${SSH_KEY_PATH:-}" ]]; then
  SSH_OPTS+=(-i "${SSH_KEY_PATH}")
  SCP_OPTS+=(-i "${SSH_KEY_PATH}")
fi

[[ -d "${PUBLISH_DIR}/agent_creator" ]] || { echo "Bundle introuvable : ${PUBLISH_DIR}/agent_creator" >&2; exit 1; }

ssh "${SSH_OPTS[@]}" "${SSH_TARGET}" "sudo mkdir -p '${APP_DIR}' '${BACKUP_DIR}' && sudo chown -R ${SSH_USER}:${SSH_USER} '${APP_ROOT}'"

ssh "${SSH_OPTS[@]}" "${SSH_TARGET}" bash -s <<REMOTE_BACKUP
set -eu
TIMESTAMP=\$(date +%Y%m%d-%H%M%S)
if [[ -d '${APP_DIR}' ]] && [[ -n "\$(ls -A '${APP_DIR}' 2>/dev/null || true)" ]]; then
  sudo cp -a '${APP_DIR}' '${BACKUP_DIR}/'\${TIMESTAMP}
  echo "Sauvegarde : ${BACKUP_DIR}/\${TIMESTAMP}"
fi
REMOTE_BACKUP

ssh "${SSH_OPTS[@]}" "${SSH_TARGET}" "mkdir -p '${STAGING_REMOTE}'"
scp "${SCP_OPTS[@]}" -r "${PUBLISH_DIR}/." "${SSH_TARGET}:${STAGING_REMOTE}/"

if [[ -n "${APP_ENV_FILE:-}" && -f "${APP_ENV_FILE}" ]]; then
  scp "${SCP_OPTS[@]}" "${APP_ENV_FILE}" "${SSH_TARGET}:/tmp/${SERVICE_NAME}.app.env"
elif [[ -n "${APP_ENV_CONTENT:-}" ]]; then
  ENV_FILE="$(mktemp)"
  umask 077
  printf '%s\n' "${APP_ENV_CONTENT}" > "${ENV_FILE}"
  scp "${SCP_OPTS[@]}" "${ENV_FILE}" "${SSH_TARGET}:/tmp/${SERVICE_NAME}.app.env"
  rm -f "${ENV_FILE}"
fi

DB_NAME="$(printf '%s' "${CONNECTION_STRING}" | sed -n 's/.*[Dd]atabase=\([^;]*\).*/\1/p')"
if [[ -z "${DB_NAME}" ]]; then
  DB_NAME="$(python3 "$(dirname "$0")/normalize_db_url.py" "${CONNECTION_STRING}" | sed -n 's|.*/\([^/?]*\).*|\1|p')"
fi
DB_NAME="${DB_NAME:-agentia}"
DB_OWNER="$(printf '%s' "${CONNECTION_STRING}" | sed -n 's/.*[Uu]sername=\([^;]*\).*/\1/p')"
if [[ -z "${DB_OWNER}" ]]; then
  DB_OWNER="$(python3 "$(dirname "$0")/normalize_db_url.py" "${CONNECTION_STRING}" | sed -n 's|.*://\([^:]*\):.*|\1|p')"
fi
DB_OWNER="${DB_OWNER:-gisedocuser}"

ssh "${SSH_OPTS[@]}" "${SSH_TARGET}" bash -s <<REMOTE_DEPLOY
set -eu
echo "Arret ${SERVICE_NAME}..."
sudo systemctl stop '${SERVICE_NAME}' || true
sleep 1

sudo rsync -a --delete \
  --exclude '.env' \
  '${STAGING_REMOTE}/' '${APP_DIR}/'
rm -rf '${STAGING_REMOTE}'

if [[ -f "/tmp/${SERVICE_NAME}.app.env" ]]; then
  sudo mv "/tmp/${SERVICE_NAME}.app.env" "${APP_DIR}/.env"
  sudo chmod 600 "${APP_DIR}/.env"
  sudo chown ${SSH_USER}:${SSH_USER} "${APP_DIR}/.env"
fi

if [[ ! -f "${APP_DIR}/.env" ]]; then
  echo "::error::.env absent — voir deploy/GITHUB-SECRETS.md"
  exit 1
fi
if ! grep -qE '^DATABASE_URL=postgres(ql([+]asyncpg)?)?://' "${APP_DIR}/.env"; then
  echo "::error::DATABASE_URL PostgreSQL requis dans .env"
  exit 1
fi

SECRETS_FILE='${APP_ROOT}/secrets.json'
if [[ ! -f "\${SECRETS_FILE}" ]]; then
  echo "::warning::\${SECRETS_FILE} absent — deploy/SERVER-SECRETS.md (JWT, Gemini)"
fi

DB_NAME='${DB_NAME}'
DB_OWNER='${DB_OWNER}'
if command -v psql >/dev/null 2>&1; then
  if ! sudo -u postgres psql -tAc "SELECT 1 FROM pg_database WHERE datname='\${DB_NAME}'" | grep -q 1; then
    echo "Creation base \${DB_NAME}..."
    sudo -u postgres psql -v ON_ERROR_STOP=1 -c "CREATE DATABASE \"\${DB_NAME}\" OWNER \${DB_OWNER};" || true
  fi
  sudo -u postgres psql -d "\${DB_NAME}" -v ON_ERROR_STOP=1 -c "GRANT ALL ON SCHEMA public TO \${DB_OWNER};" || true
fi

if [[ ! -d '${VENV_DIR}' ]]; then
  python3 -m venv '${VENV_DIR}'
fi
'${VENV_DIR}/bin/pip' install --upgrade pip
'${VENV_DIR}/bin/pip' install -r '${APP_DIR}/requirements.txt'

set -a
# shellcheck disable=SC1091
source "${APP_DIR}/.env"
set +a

echo "Initialisation base de donnees..."
'${VENV_DIR}/bin/python' -c "import asyncio; from agent_creator.db.session import init_db; asyncio.run(init_db())"

if [[ -f "${APP_DIR}/alembic.ini" ]]; then
  '${VENV_DIR}/bin/alembic' -c '${APP_DIR}/alembic.ini' upgrade head || true
fi

sudo chown -R ${SSH_USER}:${SSH_USER} '${APP_DIR}'
REMOTE_DEPLOY

SERVICE_FILE="/tmp/${SERVICE_NAME}.service"
cat > "${SERVICE_FILE}" <<EOF
[Unit]
Description=${APP_NAME}
After=network.target

[Service]
Type=simple
User=${SSH_USER}
Group=${SSH_USER}
WorkingDirectory=${APP_DIR}
Environment=PYTHONUNBUFFERED=1
EnvironmentFile=-${APP_DIR}/.env
ExecStart=${VENV_DIR}/bin/uvicorn agent_creator.main:app --host 0.0.0.0 --port ${LISTEN_PORT}
Restart=always
RestartSec=5
TimeoutStartSec=120
SyslogIdentifier=${SERVICE_NAME}
NoNewPrivileges=true
PrivateTmp=true

[Install]
WantedBy=multi-user.target
EOF

scp "${SCP_OPTS[@]}" "${SERVICE_FILE}" "${SSH_TARGET}:/tmp/${SERVICE_NAME}.service"
rm -f "${SERVICE_FILE}"

ssh "${SSH_OPTS[@]}" "${SSH_TARGET}" "sudo cp '/tmp/${SERVICE_NAME}.service' '/etc/systemd/system/${SERVICE_NAME}.service' && rm -f '/tmp/${SERVICE_NAME}.service'"
ssh "${SSH_OPTS[@]}" "${SSH_TARGET}" "sudo systemctl daemon-reload && sudo systemctl enable ${SERVICE_NAME} && sudo systemctl start ${SERVICE_NAME}"

echo "Attente démarrage ${SERVICE_NAME}..."
ssh "${SSH_OPTS[@]}" "${SSH_TARGET}" bash -s <<REMOTE_HEALTH
set -eu
PORT='${LISTEN_PORT}'
for i in \$(seq 1 30); do
  if curl -fsS -o /dev/null "http://127.0.0.1:\${PORT}/health" 2>/dev/null; then
    echo "Healthcheck OK"
    break
  fi
  if [ "\${i}" -eq 30 ]; then
    echo "::error::${SERVICE_NAME} ne répond pas sur /health"
    journalctl -u '${SERVICE_NAME}' -n 30 --no-pager || true
    exit 1
  fi
  sleep 2
done

AUTH_CODE=\$(curl -s -o /dev/null -w "%{http_code}" -X POST "http://127.0.0.1:\${PORT}/auth/register" \\
  -H "Content-Type: application/json" -d '{}' || echo "000")
case "\${AUTH_CODE}" in
  404|000)
    echo "::error::POST /auth/register → HTTP \${AUTH_CODE} (routes API indisponibles)"
    journalctl -u '${SERVICE_NAME}' -n 50 --no-pager || true
    exit 1
    ;;
  422|400|201)
    echo "API /auth/register accessible (HTTP \${AUTH_CODE})"
    ;;
  *)
    echo "::warning::POST /auth/register → HTTP \${AUTH_CODE} (attendu 422/400/201)"
    ;;
esac

if ! curl -fsS "http://127.0.0.1:\${PORT}/openapi.json" 2>/dev/null | grep -q '"/auth/register"'; then
  echo "::error::Route /auth/register absente de OpenAPI"
  exit 1
fi
echo "Verification API OK"
REMOTE_HEALTH

echo "Deploiement reussi sur ${SSH_HOST} (${SERVICE_NAME})."

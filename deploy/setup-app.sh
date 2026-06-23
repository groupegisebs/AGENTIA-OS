#!/usr/bin/env bash
set -e
APP_DIR=/opt/apps/agentia-os/app
VENV=/opt/apps/agentia-os/venv

cd "$APP_DIR"
set -a
source .env
set +a

echo '=== Init DB tables ==='
"$VENV/bin/python" -c "import asyncio; from agent_creator.db.session import init_db; asyncio.run(init_db())"
echo 'init_db OK'

echo '=== Alembic migrations ==='
if [ -f "$APP_DIR/alembic.ini" ]; then
  "$VENV/bin/alembic" -c "$APP_DIR/alembic.ini" upgrade head
  echo 'Alembic OK'
else
  echo 'No alembic.ini, skipping'
fi
echo 'setup-app done'

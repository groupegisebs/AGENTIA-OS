#!/usr/bin/env bash
# Setup PostgreSQL for Agentia OS (run once on the server)
set -euo pipefail

DB_PASS="${1:?DB_PASS requis}"

sudo -u postgres psql -v ON_ERROR_STOP=1 <<SQL
DO \$do\$
BEGIN
  IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'agentiauser') THEN
    EXECUTE format('CREATE ROLE agentiauser LOGIN PASSWORD %L', '${DB_PASS}');
  ELSE
    EXECUTE format('ALTER ROLE agentiauser PASSWORD %L', '${DB_PASS}');
  END IF;
END
\$do\$;
SELECT 'agentiauser OK';
SQL

sudo -u postgres psql -v ON_ERROR_STOP=1 <<SQL
DO \$do\$
BEGIN
  IF NOT EXISTS (SELECT FROM pg_database WHERE datname = 'agentia') THEN
    CREATE DATABASE agentia OWNER agentiauser;
  END IF;
END
\$do\$;
SQL

sudo -u postgres psql -d agentia -v ON_ERROR_STOP=1 -c "GRANT ALL ON SCHEMA public TO agentiauser;"
echo "DB agentia + user agentiauser OK"

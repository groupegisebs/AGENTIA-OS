#!/usr/bin/env python3
"""Convertit une connection string .NET (Host=...) en URL SQLAlchemy async PostgreSQL."""
from __future__ import annotations

import sys
from urllib.parse import quote_plus


def _parse_kv(raw: str) -> dict[str, str]:
    parts: dict[str, str] = {}
    for chunk in raw.split(";"):
        chunk = chunk.strip()
        if not chunk or "=" not in chunk:
            continue
        key, _, value = chunk.partition("=")
        parts[key.strip().lower()] = value.strip()
    return parts


def normalize_database_url(raw: str) -> str:
    value = raw.strip()
    if not value:
        raise SystemExit("Connection string vide")
    lower = value.lower()
    if lower.startswith(("postgresql+asyncpg://", "postgresql://", "postgres://")):
        if lower.startswith("postgresql://"):
            return value.replace("postgresql://", "postgresql+asyncpg://", 1)
        if lower.startswith("postgres://"):
            return value.replace("postgres://", "postgresql+asyncpg://", 1)
        return value

    kv = _parse_kv(value)
    host = kv.get("host") or kv.get("server") or "127.0.0.1"
    port = kv.get("port") or "5432"
    database = kv.get("database") or kv.get("initial catalog") or "agentia"
    user = kv.get("username") or kv.get("user id") or kv.get("userid") or "postgres"
    password = kv.get("password") or ""
    return f"postgresql+asyncpg://{quote_plus(user)}:{quote_plus(password)}@{host}:{port}/{database}"


if __name__ == "__main__":
    if len(sys.argv) != 2:
        raise SystemExit(f"Usage: {sys.argv[0]} '<connection-string>'")
    print(normalize_database_url(sys.argv[1]))

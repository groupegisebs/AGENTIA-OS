#!/usr/bin/env python3
"""Corrige le proxy NPM agentiafactory.gisebs.com → Web :8081 + API routes."""
import json
import sqlite3
from datetime import datetime, timezone

DB = "/var/lib/docker/volumes/npm_data/_data/database.sqlite"
HOST = "172.17.0.1"

locations = [
    {"path": "/api", "forward_scheme": "http", "forward_host": HOST, "forward_port": 8080, "advanced_config": ""},
    {"path": "/hubs", "forward_scheme": "http", "forward_host": HOST, "forward_port": 8080, "advanced_config": "proxy_read_timeout 86400s;"},
    {"path": "/swagger", "forward_scheme": "http", "forward_host": HOST, "forward_port": 8080, "advanced_config": ""},
    {"path": "/health", "forward_scheme": "http", "forward_host": HOST, "forward_port": 8080, "advanced_config": ""},
]

now = datetime.now(timezone.utc).strftime("%Y-%m-%d %H:%M:%S")
conn = sqlite3.connect(DB)
conn.execute(
    """UPDATE proxy_host SET
        forward_host = ?,
        forward_port = 8081,
        locations = ?,
        modified_on = ?,
        allow_websocket_upgrade = 1
    WHERE id = 10""",
    (HOST, json.dumps(locations), now),
)
conn.commit()
row = conn.execute(
    "SELECT domain_names, forward_host, forward_port, locations FROM proxy_host WHERE id=10"
).fetchone()
print("OK:", row)

#!/usr/bin/env python3
"""Vérifie que les routes SPA répondent (usage: PYTHONPATH=. python scripts/check-routes.py)."""
import asyncio
import sys

from httpx import ASGITransport, AsyncClient

from agent_creator.main import SPA_INDEX, app


async def main() -> int:
    print(f"SPA index: {SPA_INDEX} (exists={SPA_INDEX.is_file()})")
    transport = ASGITransport(app=app)
    async with AsyncClient(transport=transport, base_url="http://test") as client:
        for path in ("/connexion", "/inscription", "/", "/health", "/docs"):
            response = await client.get(path)
            ctype = response.headers.get("content-type", "")
            print(f"GET {path} -> {response.status_code} [{ctype[:40]}]")
            if path in ("/connexion", "/inscription", "/") and response.status_code != 200:
                return 1
            if path in ("/connexion", "/inscription", "/") and "html" not in ctype:
                return 1
    return 0


if __name__ == "__main__":
    sys.exit(asyncio.run(main()))

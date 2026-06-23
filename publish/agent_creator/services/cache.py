"""Cache Redis : manifestes agents (dormance/réveil) + résultats d'invocations async.

Cycle de vie d'un agent dormant
────────────────────────────────
1. Dormant  : manifest stocké uniquement en PostgreSQL (0 mémoire/CPU)
2. Réveil   : première invocation → manifest chargé de DB et mis en cache Redis (< 1 ms suivants)
3. Actif    : manifest chaud en Redis (TTL 1 h), invocations traitées par les workers ARQ
4. Endormi  : pas d'invocation pendant 1 h → clé Redis expirée → retour à l'état dormant
"""

from __future__ import annotations

import json
import logging
from typing import Any

logger = logging.getLogger(__name__)

# TTL (secondes)
MANIFEST_TTL = 3600   # 1 h  — agent « chaud »
RUN_TTL = 300         # 5 min — résultat d'un run asynchrone


class AgentCache:
    """Façade Redis pour la dormance des agents et les résultats de runs."""

    def __init__(self, client: Any) -> None:
        self._r = client

    # ─── Manifests (dormance / réveil) ───────────────────────────────────────

    async def get_manifest(self, agent_id: str) -> dict | None:
        """Retourne le manifest depuis Redis ou None si l'agent est dormant."""
        raw = await self._r.get(f"agent:manifest:{agent_id}")
        if raw:
            logger.debug("Agent %s — réveil depuis cache Redis", agent_id)
        return json.loads(raw) if raw else None

    async def set_manifest(self, agent_id: str, manifest_dict: dict) -> None:
        """Met le manifest en cache (l'agent devient « chaud »)."""
        await self._r.setex(
            f"agent:manifest:{agent_id}",
            MANIFEST_TTL,
            json.dumps(manifest_dict),
        )
        logger.debug("Agent %s — manifest mis en cache (%ds TTL)", agent_id, MANIFEST_TTL)

    async def invalidate_manifest(self, agent_id: str) -> None:
        """Invalide le cache lors d'une mise à jour de l'agent."""
        await self._r.delete(f"agent:manifest:{agent_id}")

    # ─── Runs asynchrones (résultats + statuts) ───────────────────────────────

    async def set_run_status(self, run_id: str, status: str, **extra: Any) -> None:
        payload = {"status": status, "run_id": run_id, **extra}
        await self._r.setex(f"run:status:{run_id}", RUN_TTL, json.dumps(payload))

    async def get_run_status(self, run_id: str) -> dict | None:
        raw = await self._r.get(f"run:status:{run_id}")
        return json.loads(raw) if raw else None

    async def set_run_result(self, run_id: str, result: dict) -> None:
        await self._r.setex(f"run:result:{run_id}", RUN_TTL, json.dumps(result))

    async def get_run_result(self, run_id: str) -> dict | None:
        raw = await self._r.get(f"run:result:{run_id}")
        return json.loads(raw) if raw else None

    # ─── Santé ───────────────────────────────────────────────────────────────

    async def ping(self) -> bool:
        try:
            return bool(await self._r.ping())
        except Exception:
            return False

    async def close(self) -> None:
        try:
            await self._r.aclose()
        except Exception:
            pass

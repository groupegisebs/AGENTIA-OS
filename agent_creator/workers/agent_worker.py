"""Worker ARQ : exécution asynchrone des agents publiés.

Modèle de dormance
──────────────────
• Un agent dormant = un manifest JSON en PostgreSQL (0 CPU/RAM)
• Réveil  = première invocation → manifest chargé depuis DB → mis en cache Redis
• Actif   = invocations suivantes servent le cache Redis (< 1 ms de latence)
• Endormi = TTL Redis expiré après 1 h sans invocation

Scalabilité
───────────
• 1 worker  = max_jobs=100  → 100 agents simultanés (I/O bound, asyncio)
• 10 workers                → 1 000 agents simultanés
• docker compose up -d --scale worker=10
"""

from __future__ import annotations

import json
import logging
import os
import time
from datetime import datetime

import redis.asyncio as aioredis
from arq.connections import RedisSettings

from agent_creator.config import get_settings
from agent_creator.db.repository import DbStore
from agent_creator.db.session import async_session_factory
from agent_creator.models.agent import AgentInvocation, AgentManifest
from agent_creator.services.cache import MANIFEST_TTL, AgentCache
from agent_creator.services.llm import LLMService

logger = logging.getLogger(__name__)


# ─── Tâche principale ─────────────────────────────────────────────────────────

async def execute_agent_run(
    ctx: dict,
    run_id: str,
    agent_id: str,
    organization_id: str,
    message: str,
) -> dict:
    """Réveille l'agent, appelle le LLM, stocke le résultat dans Redis.

    Appelé par ARQ depuis la queue Redis.
    ctx["cache"]    : AgentCache
    ctx["llm"]      : LLMService
    """
    cache: AgentCache = ctx["cache"]
    llm: LLMService = ctx["llm"]

    await cache.set_run_status(run_id, "running")

    # ── 1. Charger le manifest (réveil de l'agent dormant) ───────────────────
    manifest_dict = await cache.get_manifest(agent_id)
    if not manifest_dict:
        # Dormant → chargement depuis PostgreSQL + mise en cache
        factory = async_session_factory()
        async with factory() as session:
            db = DbStore(session)
            agent = await db.get_published_agent(agent_id)
        if not agent:
            result = {"status": "error", "run_id": run_id, "error": "Agent introuvable"}
            await cache.set_run_result(run_id, result)
            return result
        manifest_dict = agent.manifest.model_dump(mode="json")
        await cache.set_manifest(agent_id, manifest_dict)
        logger.info("Agent %s réveillé depuis DB et mis en cache", agent_id)

    manifest = AgentManifest.model_validate(manifest_dict)
    policies = manifest.policies

    if len(message) > policies.max_input_chars:
        result = {
            "status": "error",
            "run_id": run_id,
            "error": f"Message trop long ({len(message)} car.) — limite : {policies.max_input_chars}.",
        }
        await cache.set_run_result(run_id, result)
        return result

    # ── 2. Appel LLM (async I/O) ─────────────────────────────────────────────
    messages = [
        {"role": "system", "content": manifest.system_prompt},
        {"role": "user", "content": message},
    ]
    t0 = time.monotonic()
    llm_status = "success"
    reply = ""
    error_msg = ""

    try:
        reply = await llm.chat(messages)
    except Exception as exc:
        llm_status = "error"
        error_msg = str(exc)
        logger.error("Erreur LLM — agent %s run %s : %s", agent_id, run_id, exc)

    latency_ms = int((time.monotonic() - t0) * 1000)

    # ── 3. Persister l'invocation en base ────────────────────────────────────
    inv = AgentInvocation(
        id=run_id,
        agent_id=agent_id,
        organization_id=organization_id,
        input_chars=len(message),
        latency_ms=latency_ms,
        status=llm_status,
        created_at=datetime.utcnow(),
    )
    try:
        factory = async_session_factory()
        async with factory() as session:
            db = DbStore(session)
            await db.save_agent_invocation(inv)
    except Exception as exc:
        logger.error("Impossible de sauvegarder l'invocation %s : %s", run_id, exc)

    # ── 4. Publier le résultat dans Redis (lu par SSE / polling) ─────────────
    if llm_status == "success":
        result = {
            "status": "done",
            "run_id": run_id,
            "agent_id": agent_id,
            "reply": reply,
            "latency_ms": latency_ms,
        }
    else:
        result = {"status": "error", "run_id": run_id, "error": error_msg}

    await cache.set_run_result(run_id, result)
    logger.info("Run %s terminé en %d ms (statut: %s)", run_id, latency_ms, llm_status)
    return result


# ─── Lifecycle du worker ──────────────────────────────────────────────────────

async def startup(ctx: dict) -> None:
    """Connexions Redis, DB engine, LLM service."""
    settings = get_settings()
    redis_client = aioredis.from_url(
        settings.redis_url,
        encoding="utf-8",
        decode_responses=True,
    )
    ctx["cache"] = AgentCache(redis_client)
    ctx["llm"] = LLMService(settings)
    ctx["settings"] = settings

    # Initialise le moteur SQLAlchemy (partagé entre les coroutines du worker)
    from agent_creator.db.session import get_engine
    get_engine()

    logger.info(
        "Worker ARQ démarré — LLM : %s — Redis : %s",
        ctx["llm"].mode_label,
        settings.redis_url,
    )


async def shutdown(ctx: dict) -> None:
    await ctx["cache"].close()
    logger.info("Worker ARQ arrêté")


# ─── Configuration ARQ ────────────────────────────────────────────────────────

class WorkerSettings:
    functions = [execute_agent_run]
    on_startup = startup
    on_shutdown = shutdown

    # 100 coroutines asyncio par worker (I/O bound → efficace)
    # Pour 1000 agents simultanés : docker compose up --scale worker=10
    max_jobs = 100

    # Timeout max par invocation LLM (2 min)
    job_timeout = 120

    # Les résultats sont dans Redis (AgentCache) — pas besoin du keep_result ARQ
    keep_result = 0

    redis_settings = RedisSettings.from_dsn(
        os.environ.get("REDIS_URL", "redis://127.0.0.1:6379")
    )

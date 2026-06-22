"""Sécurité consommation : isolation tenant, rate limit, clés API."""

import hashlib
import secrets
from datetime import datetime, timedelta

from fastapi import HTTPException

from agent_creator.db.repository import DbStore
from agent_creator.models.agent import AgentApiKey, PublishedAgent

_KEY_PREFIX = "agt_"
_KEY_BYTES = 32


def generate_api_key() -> tuple[str, str]:
    """Retourne (clé en clair, hash SHA-256 à stocker)."""
    raw = _KEY_PREFIX + secrets.token_urlsafe(_KEY_BYTES)
    digest = hashlib.sha256(raw.encode()).hexdigest()
    return raw, digest


def hash_api_key(raw: str) -> str:
    return hashlib.sha256(raw.encode()).hexdigest()


async def create_agent_api_key(db: DbStore, agent_id: str, label: str) -> tuple[AgentApiKey, str]:
    raw, digest = generate_api_key()
    key = AgentApiKey(
        agent_id=agent_id,
        label=label,
        key_hash=digest,
        created_at=datetime.utcnow(),
    )
    await db.save_agent_api_key(key)
    return key, raw


async def lookup_api_key(db: DbStore, raw_key: str) -> AgentApiKey | None:
    if not raw_key.startswith(_KEY_PREFIX):
        return None
    digest = hash_api_key(raw_key)
    key = await db.get_agent_api_key_by_hash(digest)
    if key and key.is_active:
        return key
    return None


def check_agent_access(agent: PublishedAgent, organization_id: str) -> None:
    """Lève 403 si l'org ne peut pas accéder à cet agent."""
    if not agent.is_accessible_by(organization_id):
        raise HTTPException(status_code=403, detail="Accès refusé à cet agent.")


async def check_rate_limit(db: DbStore, agent: PublishedAgent, organization_id: str) -> None:
    """Lève 429 si le quota horaire est dépassé."""
    limit = agent.manifest.policies.max_requests_per_hour
    if limit <= 0:
        return
    since = datetime.utcnow() - timedelta(hours=1)
    count = await db.count_recent_invocations(agent.id, organization_id, since)
    if count >= limit:
        raise HTTPException(
            status_code=429,
            detail=f"Quota dépassé ({limit} req/h). Réessayez dans une heure.",
        )

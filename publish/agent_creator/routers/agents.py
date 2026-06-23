"""Routes : agents publiés, invocation (sync + async), clés API, marketplace."""

import asyncio
import json
from datetime import datetime
from uuid import uuid4

from fastapi import APIRouter, Depends, HTTPException, Request
from pydantic import BaseModel
from sse_starlette.sse import EventSourceResponse

from agent_creator.db.repository import DbStore
from agent_creator.dependencies import UserContext, get_agent_consumer, get_current_user, get_db_store
from agent_creator.models.agent import AgentManifest, AgentStatus, AgentVisibility, InvocationRequest, PublishedAgent
from agent_creator.services.agent_runtime import invoke_agent
from agent_creator.services.agent_security import check_agent_access, check_rate_limit, create_agent_api_key
from agent_creator.services.cache import AgentCache

router = APIRouter(prefix="/agents", tags=["agents"])
marketplace_router = APIRouter(prefix="/marketplace", tags=["marketplace"])


# ─── Response schemas ────────────────────────────────────────────────────────

class AgentResponse(BaseModel):
    id: str
    organization_id: str
    deployment_id: str
    blueprint_id: str
    title: str
    description: str
    category: str
    visibility: AgentVisibility
    status: AgentStatus
    manifest: AgentManifest
    created_at: datetime
    updated_at: datetime

    @classmethod
    def from_agent(cls, agent: PublishedAgent) -> "AgentResponse":
        return cls(**agent.model_dump())


class AgentSummaryResponse(BaseModel):
    """Vue allégée pour les listings (sans system_prompt)."""
    id: str
    organization_id: str
    deployment_id: str
    title: str
    description: str
    category: str
    visibility: AgentVisibility
    status: AgentStatus
    created_at: datetime

    @classmethod
    def from_agent(cls, agent: PublishedAgent) -> "AgentSummaryResponse":
        return cls(
            id=agent.id,
            organization_id=agent.organization_id,
            deployment_id=agent.deployment_id,
            title=agent.title,
            description=agent.description,
            category=agent.category,
            visibility=agent.visibility,
            status=agent.status,
            created_at=agent.created_at,
        )


class UpdateAgentRequest(BaseModel):
    visibility: AgentVisibility | None = None
    description: str | None = None
    status: AgentStatus | None = None


class CreateApiKeyRequest(BaseModel):
    label: str = ""


class ApiKeyCreatedResponse(BaseModel):
    id: str
    agent_id: str
    label: str
    key: str
    created_at: datetime


class ApiKeyListItem(BaseModel):
    id: str
    agent_id: str
    label: str
    created_at: datetime
    revoked_at: datetime | None


class RunResponse(BaseModel):
    """Réponse 202 : run asynchrone enqueued."""
    run_id: str
    agent_id: str
    status: str  # "queued"


class RunResult(BaseModel):
    """Résultat d'un run (polling ou SSE)."""
    status: str   # "done" | "error" | "running" | "queued"
    run_id: str
    reply: str | None = None
    agent_id: str | None = None
    latency_ms: int | None = None
    error: str | None = None


def _get_cache(request: Request) -> AgentCache | None:
    return getattr(request.app.state, "cache", None)


def _get_arq_pool(request: Request):
    return getattr(request.app.state, "arq_pool", None)


# ─── Agent routes ────────────────────────────────────────────────────────────

@router.get("", response_model=list[AgentSummaryResponse])
async def list_agents(
    ctx: UserContext = Depends(get_current_user),
    db: DbStore = Depends(get_db_store),
) -> list[AgentSummaryResponse]:
    agents = await db.list_published_agents(ctx.organization.id)
    return [AgentSummaryResponse.from_agent(a) for a in agents]


@router.get("/{agent_id}", response_model=AgentResponse)
async def get_agent(
    agent_id: str,
    ctx: UserContext = Depends(get_current_user),
    db: DbStore = Depends(get_db_store),
) -> AgentResponse:
    agent = await db.get_published_agent(agent_id)
    if not agent:
        raise HTTPException(status_code=404, detail="Agent introuvable.")
    check_agent_access(agent, ctx.organization.id)
    return AgentResponse.from_agent(agent)


@router.patch("/{agent_id}", response_model=AgentResponse)
async def update_agent(
    agent_id: str,
    body: UpdateAgentRequest,
    ctx: UserContext = Depends(get_current_user),
    db: DbStore = Depends(get_db_store),
) -> AgentResponse:
    agent = await db.get_published_agent(agent_id)
    if not agent:
        raise HTTPException(status_code=404, detail="Agent introuvable.")
    if agent.organization_id != ctx.organization.id:
        raise HTTPException(status_code=403, detail="Seul le créateur peut modifier cet agent.")
    if body.visibility is not None:
        agent.visibility = body.visibility
    if body.description is not None:
        agent.description = body.description
    if body.status is not None:
        agent.status = body.status
    agent.updated_at = datetime.utcnow()
    await db.save_published_agent(agent)
    return AgentResponse.from_agent(agent)


@router.post("/{agent_id}/invoke", response_model=dict)
async def invoke(
    agent_id: str,
    body: InvocationRequest,
    ctx: UserContext = Depends(get_agent_consumer),
    db: DbStore = Depends(get_db_store),
) -> dict:
    agent = await db.get_published_agent(agent_id)
    if not agent:
        raise HTTPException(status_code=404, detail="Agent introuvable.")
    check_agent_access(agent, ctx.organization.id)
    await check_rate_limit(db, agent, ctx.organization.id)

    from agent_creator.main import llm

    result = await invoke_agent(agent, body.message, ctx.organization.id, db, llm)
    return result.model_dump()


@router.post("/{agent_id}/run", response_model=RunResponse, status_code=202)
async def run_agent(
    agent_id: str,
    body: InvocationRequest,
    request: Request,
    ctx: UserContext = Depends(get_agent_consumer),
    db: DbStore = Depends(get_db_store),
) -> RunResponse:
    """Invocation **asynchrone** : retourne immédiatement un run_id.

    Le résultat est livré par :
    - Polling : GET /agents/{id}/runs/{run_id}
    - SSE     : GET /agents/{id}/runs/{run_id}/stream
    """
    arq_pool = _get_arq_pool(request)
    if arq_pool is None:
        raise HTTPException(
            status_code=503,
            detail="Workers non disponibles. Redis requis — vérifiez que le service Redis est démarré.",
        )
    agent = await db.get_published_agent(agent_id)
    if not agent:
        raise HTTPException(status_code=404, detail="Agent introuvable.")
    check_agent_access(agent, ctx.organization.id)
    await check_rate_limit(db, agent, ctx.organization.id)

    cache: AgentCache = _get_cache(request)
    run_id = str(uuid4())

    # Pré-chauffer le cache (réveil immédiat de l'agent dans le worker)
    if cache:
        await cache.set_manifest(agent_id, agent.manifest.model_dump(mode="json"))
        await cache.set_run_status(run_id, "queued")

    await arq_pool.enqueue_job(
        "execute_agent_run",
        run_id,
        agent_id,
        ctx.organization.id,
        body.message,
    )
    return RunResponse(run_id=run_id, agent_id=agent_id, status="queued")


@router.get("/{agent_id}/runs/{run_id}", response_model=RunResult)
async def get_run(
    agent_id: str,
    run_id: str,
    request: Request,
    ctx: UserContext = Depends(get_agent_consumer),
) -> RunResult:
    """Polling : récupère le résultat d'un run asynchrone."""
    cache = _get_cache(request)
    if cache is None:
        raise HTTPException(status_code=503, detail="Redis non disponible.")

    result = await cache.get_run_result(run_id)
    if result:
        return RunResult(**result)

    status_data = await cache.get_run_status(run_id)
    if status_data:
        return RunResult(**status_data)

    raise HTTPException(status_code=404, detail="Run introuvable (expiré ou inexistant).")


@router.get("/{agent_id}/runs/{run_id}/stream")
async def stream_run(
    agent_id: str,
    run_id: str,
    request: Request,
    ctx: UserContext = Depends(get_agent_consumer),
) -> EventSourceResponse:
    """SSE : pousse le résultat dès que le worker a terminé (max 2 min).

    Événements émis :
    - heartbeat : {"status": "running"|"queued"}   (toutes les 500 ms)
    - done      : {"status": "done", "reply": "...", "latency_ms": N}
    - error     : {"status": "error", "error": "..."}
    """
    cache = _get_cache(request)
    if cache is None:
        raise HTTPException(status_code=503, detail="Redis non disponible.")

    async def event_generator():
        for _ in range(240):  # 240 × 500 ms = 120 s max
            if await request.is_disconnected():
                return

            result = await cache.get_run_result(run_id)
            if result:
                event = "done" if result.get("status") == "done" else "error"
                yield {"event": event, "data": json.dumps(result)}
                return

            status_data = await cache.get_run_status(run_id)
            yield {
                "event": "heartbeat",
                "data": json.dumps(status_data or {"status": "queued", "run_id": run_id}),
            }
            await asyncio.sleep(0.5)

        yield {
            "event": "error",
            "data": json.dumps({"status": "timeout", "run_id": run_id, "error": "Délai max dépassé"}),
        }

    return EventSourceResponse(event_generator())


@router.post("/{agent_id}/api-keys", response_model=ApiKeyCreatedResponse, status_code=201)
async def create_api_key(
    agent_id: str,
    body: CreateApiKeyRequest,
    ctx: UserContext = Depends(get_current_user),
    db: DbStore = Depends(get_db_store),
) -> ApiKeyCreatedResponse:
    agent = await db.get_published_agent(agent_id)
    if not agent:
        raise HTTPException(status_code=404, detail="Agent introuvable.")
    if agent.organization_id != ctx.organization.id:
        raise HTTPException(status_code=403, detail="Seul le créateur peut gérer les clés de cet agent.")
    key_obj, raw = await create_agent_api_key(db, agent_id, body.label)
    return ApiKeyCreatedResponse(
        id=key_obj.id,
        agent_id=key_obj.agent_id,
        label=key_obj.label,
        key=raw,
        created_at=key_obj.created_at,
    )


@router.get("/{agent_id}/api-keys", response_model=list[ApiKeyListItem])
async def list_api_keys(
    agent_id: str,
    ctx: UserContext = Depends(get_current_user),
    db: DbStore = Depends(get_db_store),
) -> list[ApiKeyListItem]:
    agent = await db.get_published_agent(agent_id)
    if not agent:
        raise HTTPException(status_code=404, detail="Agent introuvable.")
    if agent.organization_id != ctx.organization.id:
        raise HTTPException(status_code=403, detail="Accès refusé.")
    keys = await db.list_agent_api_keys(agent_id)
    return [
        ApiKeyListItem(id=k.id, agent_id=k.agent_id, label=k.label, created_at=k.created_at, revoked_at=k.revoked_at)
        for k in keys
    ]


@router.delete("/{agent_id}/api-keys/{key_id}", status_code=204)
async def revoke_api_key(
    agent_id: str,
    key_id: str,
    ctx: UserContext = Depends(get_current_user),
    db: DbStore = Depends(get_db_store),
) -> None:
    agent = await db.get_published_agent(agent_id)
    if not agent:
        raise HTTPException(status_code=404, detail="Agent introuvable.")
    if agent.organization_id != ctx.organization.id:
        raise HTTPException(status_code=403, detail="Accès refusé.")
    keys = await db.list_agent_api_keys(agent_id)
    key = next((k for k in keys if k.id == key_id), None)
    if not key:
        raise HTTPException(status_code=404, detail="Clé introuvable.")
    key.revoked_at = datetime.utcnow()
    await db.save_agent_api_key(key)


# ─── Marketplace routes ───────────────────────────────────────────────────────

@marketplace_router.get("/agents", response_model=list[AgentSummaryResponse])
async def list_marketplace_agents(
    ctx: UserContext = Depends(get_current_user),
    db: DbStore = Depends(get_db_store),
) -> list[AgentSummaryResponse]:
    """Agents publics de toutes les organisations."""
    agents = await db.list_public_agents()
    return [AgentSummaryResponse.from_agent(a) for a in agents]

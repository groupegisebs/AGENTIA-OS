"""Routes : agents publiés, invocation, clés API, marketplace."""

from datetime import datetime

from fastapi import APIRouter, Depends, HTTPException
from pydantic import BaseModel

from agent_creator.db.repository import DbStore
from agent_creator.dependencies import UserContext, get_agent_consumer, get_current_user, get_db_store
from agent_creator.models.agent import AgentManifest, AgentStatus, AgentVisibility, InvocationRequest, PublishedAgent
from agent_creator.services.agent_runtime import invoke_agent
from agent_creator.services.agent_security import check_agent_access, check_rate_limit, create_agent_api_key

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

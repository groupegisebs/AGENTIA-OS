from fastapi import APIRouter, Depends, HTTPException, Query
from pydantic import BaseModel, Field

from agent_creator.db.repository import DbStore
from agent_creator.dependencies import (
    UserContext,
    get_agent_os_service,
    get_current_user,
    get_db_store,
)
from agent_creator.models.agent import PublishedAgent
from agent_creator.models.os_runtime import (
    AgentCapabilityRegistry,
    AgentEvent,
    AgentLifecycleState,
    AgentMemoryEntry,
    AgentRuntime,
)
from agent_creator.services.os_foundation import AgentOSService

router = APIRouter(prefix="/os/v1", tags=["os-runtime"])


class RuntimeStateUpdateRequest(BaseModel):
    state: AgentLifecycleState
    reason: str | None = None
    error: str | None = None


class CapabilityRegistryUpdateRequest(BaseModel):
    tools: list[str] = Field(default_factory=list)
    actions: list[str] = Field(default_factory=list)
    events: list[str] = Field(default_factory=list)


class PublishEventRequest(BaseModel):
    event_type: str = Field(..., min_length=1, max_length=64)
    payload: dict = Field(default_factory=dict)
    metadata: dict = Field(default_factory=dict)


class AddMemoryEntryRequest(BaseModel):
    namespace: str = Field(default="default", min_length=1, max_length=64)
    text: str = Field(..., min_length=1)
    metadata: dict = Field(default_factory=dict)


async def _get_owned_agent(db: DbStore, agent_id: str, organization_id: str) -> PublishedAgent:
    agent = await db.get_published_agent(agent_id)
    if not agent:
        raise HTTPException(status_code=404, detail="Agent introuvable.")
    if agent.organization_id != organization_id:
        raise HTTPException(status_code=403, detail="Accès refusé pour ce tenant.")
    return agent


@router.get("/agents/{agent_id}/runtime", response_model=AgentRuntime)
async def get_agent_runtime(
    agent_id: str,
    ctx: UserContext = Depends(get_current_user),
    db: DbStore = Depends(get_db_store),
    os_service: AgentOSService = Depends(get_agent_os_service),
) -> AgentRuntime:
    await _get_owned_agent(db, agent_id, ctx.organization.id)
    return await os_service.get_runtime(db, agent_id, ctx.organization.id)


@router.put("/agents/{agent_id}/runtime", response_model=AgentRuntime)
async def update_agent_runtime(
    agent_id: str,
    body: RuntimeStateUpdateRequest,
    ctx: UserContext = Depends(get_current_user),
    db: DbStore = Depends(get_db_store),
    os_service: AgentOSService = Depends(get_agent_os_service),
) -> AgentRuntime:
    await _get_owned_agent(db, agent_id, ctx.organization.id)
    return await os_service.set_runtime_state(
        db,
        agent_id=agent_id,
        organization_id=ctx.organization.id,
        lifecycle_state=body.state,
        reason=body.reason,
        error=body.error,
    )


@router.get("/agents/{agent_id}/capabilities", response_model=AgentCapabilityRegistry)
async def get_agent_capabilities(
    agent_id: str,
    ctx: UserContext = Depends(get_current_user),
    db: DbStore = Depends(get_db_store),
    os_service: AgentOSService = Depends(get_agent_os_service),
) -> AgentCapabilityRegistry:
    await _get_owned_agent(db, agent_id, ctx.organization.id)
    return await os_service.get_capabilities(db, agent_id, ctx.organization.id)


@router.put("/agents/{agent_id}/capabilities", response_model=AgentCapabilityRegistry)
async def update_agent_capabilities(
    agent_id: str,
    body: CapabilityRegistryUpdateRequest,
    ctx: UserContext = Depends(get_current_user),
    db: DbStore = Depends(get_db_store),
    os_service: AgentOSService = Depends(get_agent_os_service),
) -> AgentCapabilityRegistry:
    await _get_owned_agent(db, agent_id, ctx.organization.id)
    return await os_service.set_capabilities(
        db,
        agent_id=agent_id,
        organization_id=ctx.organization.id,
        tools=body.tools,
        actions=body.actions,
        events=body.events,
    )


@router.post("/agents/{agent_id}/events", response_model=AgentEvent, status_code=201)
async def publish_agent_event(
    agent_id: str,
    body: PublishEventRequest,
    ctx: UserContext = Depends(get_current_user),
    db: DbStore = Depends(get_db_store),
    os_service: AgentOSService = Depends(get_agent_os_service),
) -> AgentEvent:
    await _get_owned_agent(db, agent_id, ctx.organization.id)
    return await os_service.publish_event(
        db,
        agent_id=agent_id,
        organization_id=ctx.organization.id,
        event_type=body.event_type,
        payload=body.payload,
        metadata=body.metadata,
    )


@router.get("/agents/{agent_id}/events", response_model=list[AgentEvent])
async def list_agent_events(
    agent_id: str,
    limit: int = Query(default=50, ge=1, le=200),
    ctx: UserContext = Depends(get_current_user),
    db: DbStore = Depends(get_db_store),
    os_service: AgentOSService = Depends(get_agent_os_service),
) -> list[AgentEvent]:
    await _get_owned_agent(db, agent_id, ctx.organization.id)
    return await os_service.list_events(db, agent_id=agent_id, limit=limit)


@router.post("/agents/{agent_id}/memory", response_model=AgentMemoryEntry, status_code=201)
async def add_agent_memory_entry(
    agent_id: str,
    body: AddMemoryEntryRequest,
    ctx: UserContext = Depends(get_current_user),
    db: DbStore = Depends(get_db_store),
    os_service: AgentOSService = Depends(get_agent_os_service),
) -> AgentMemoryEntry:
    await _get_owned_agent(db, agent_id, ctx.organization.id)
    return await os_service.append_memory(
        db,
        agent_id=agent_id,
        organization_id=ctx.organization.id,
        namespace=body.namespace,
        text=body.text,
        metadata=body.metadata,
    )


@router.get("/agents/{agent_id}/memory", response_model=list[AgentMemoryEntry])
async def list_agent_memory(
    agent_id: str,
    namespace: str | None = Query(default=None, min_length=1, max_length=64),
    limit: int = Query(default=50, ge=1, le=200),
    ctx: UserContext = Depends(get_current_user),
    db: DbStore = Depends(get_db_store),
    os_service: AgentOSService = Depends(get_agent_os_service),
) -> list[AgentMemoryEntry]:
    await _get_owned_agent(db, agent_id, ctx.organization.id)
    return await os_service.list_memory(db, agent_id=agent_id, namespace=namespace, limit=limit)

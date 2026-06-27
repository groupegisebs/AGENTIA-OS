from __future__ import annotations

from datetime import datetime
from enum import Enum
from uuid import uuid4

from pydantic import BaseModel, Field


class AgentLifecycleState(str, Enum):
    STOPPED = "stopped"
    RUNNING = "running"
    SUSPENDED = "suspended"
    FAILED = "failed"


class AgentRuntime(BaseModel):
    agent_id: str
    organization_id: str
    lifecycle_state: AgentLifecycleState = AgentLifecycleState.STOPPED
    last_error: str | None = None
    started_at: datetime | None = None
    suspended_at: datetime | None = None
    stopped_at: datetime | None = None
    created_at: datetime = Field(default_factory=datetime.utcnow)
    updated_at: datetime = Field(default_factory=datetime.utcnow)


class AgentCapabilityRegistry(BaseModel):
    agent_id: str
    organization_id: str
    tools: list[str] = Field(default_factory=list)
    actions: list[str] = Field(default_factory=list)
    events: list[str] = Field(default_factory=list)
    created_at: datetime = Field(default_factory=datetime.utcnow)
    updated_at: datetime = Field(default_factory=datetime.utcnow)


class AgentEvent(BaseModel):
    id: str = Field(default_factory=lambda: str(uuid4()))
    agent_id: str
    organization_id: str
    event_type: str
    payload: dict = Field(default_factory=dict)
    metadata: dict = Field(default_factory=dict)
    created_at: datetime = Field(default_factory=datetime.utcnow)


class AgentMemoryEntry(BaseModel):
    id: str = Field(default_factory=lambda: str(uuid4()))
    agent_id: str
    organization_id: str
    namespace: str = "default"
    text: str
    metadata: dict = Field(default_factory=dict)
    created_at: datetime = Field(default_factory=datetime.utcnow)

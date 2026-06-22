from __future__ import annotations

from datetime import datetime
from enum import Enum
from uuid import uuid4

from pydantic import BaseModel, Field


class AgentVisibility(str, Enum):
    PRIVATE = "private"
    ORGANIZATION = "organization"
    PUBLIC = "public"


class AgentStatus(str, Enum):
    ACTIVE = "active"
    PAUSED = "paused"
    ARCHIVED = "archived"


class AgentPolicies(BaseModel):
    max_input_chars: int = 4000
    max_requests_per_hour: int = 60
    pii_filter: bool = True


class AgentManifest(BaseModel):
    """Configuration exécutable dérivée d'un blueprint."""

    version: str = "1"
    system_prompt: str
    llm_provider: str = "auto"
    model: str = ""
    allowed_tools: list[str] = Field(default_factory=list)
    policies: AgentPolicies = Field(default_factory=AgentPolicies)
    components: list[dict] = Field(default_factory=list)
    category: str = "Général"
    domain: str | None = None


class PublishedAgent(BaseModel):
    """Agent publié et prêt à l'invocation."""

    id: str = Field(default_factory=lambda: str(uuid4()))
    organization_id: str
    deployment_id: str
    blueprint_id: str
    title: str
    description: str = ""
    category: str = "Général"
    visibility: AgentVisibility = AgentVisibility.PRIVATE
    status: AgentStatus = AgentStatus.ACTIVE
    manifest: AgentManifest
    created_at: datetime = Field(default_factory=datetime.utcnow)
    updated_at: datetime = Field(default_factory=datetime.utcnow)

    def is_accessible_by(self, organization_id: str) -> bool:
        if self.status != AgentStatus.ACTIVE:
            return False
        if self.visibility == AgentVisibility.PUBLIC:
            return True
        return self.organization_id == organization_id


class AgentApiKey(BaseModel):
    id: str = Field(default_factory=lambda: str(uuid4()))
    agent_id: str
    label: str = ""
    key_hash: str
    created_at: datetime = Field(default_factory=datetime.utcnow)
    revoked_at: datetime | None = None

    @property
    def is_active(self) -> bool:
        return self.revoked_at is None


class AgentInvocation(BaseModel):
    id: str = Field(default_factory=lambda: str(uuid4()))
    agent_id: str
    organization_id: str
    input_chars: int = 0
    latency_ms: int = 0
    status: str = "success"
    created_at: datetime = Field(default_factory=datetime.utcnow)


class InvocationRequest(BaseModel):
    message: str = Field(..., min_length=1)


class InvocationResponse(BaseModel):
    invocation_id: str
    reply: str
    agent_id: str
    latency_ms: int

from datetime import datetime
from enum import Enum
from uuid import uuid4

from pydantic import BaseModel, Field


class DeploymentStatus(str, Enum):
    PENDING = "pending"
    BILLING = "billing"
    DEPLOYED = "deployed"
    FAILED = "failed"
    BLOCKED = "blocked"


class Deployment(BaseModel):
    """Déploiement d'un agent/blueprint — action facturable."""

    id: str = Field(default_factory=lambda: str(uuid4()))
    organization_id: str
    conversation_id: str
    blueprint_id: str
    status: DeploymentStatus = DeploymentStatus.PENDING
    deployment_cost: float = Field(..., ge=0, description="Montant facturé en EUR")
    currency: str = "EUR"
    complexity_score: float = Field(default=1.0, ge=0)
    billed_at: datetime | None = None
    billing_event_id: str | None = None
    error_message: str | None = None
    created_at: datetime = Field(default_factory=datetime.utcnow)

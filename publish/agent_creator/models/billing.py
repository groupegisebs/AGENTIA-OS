from datetime import datetime
from enum import Enum
from uuid import uuid4

from pydantic import BaseModel, Field


class BillingEventType(str, Enum):
    DEPLOYMENT_CHARGE = "deployment_charge"
    SUBSCRIPTION = "subscription"
    SUBSCRIPTION_RENEWAL = "subscription_renewal"
    REFUND = "refund"


class BillingEventStatus(str, Enum):
    PENDING = "pending"
    SUCCEEDED = "succeeded"
    FAILED = "failed"


class BillingEvent(BaseModel):
    """Enregistrement de facturation (déploiement ou abonnement)."""

    id: str = Field(default_factory=lambda: str(uuid4()))
    organization_id: str
    event_type: BillingEventType
    amount: float = Field(..., ge=0)
    currency: str = "EUR"
    status: BillingEventStatus = BillingEventStatus.PENDING
    deployment_id: str | None = None
    payment_provider_charge_id: str | None = None
    checkout_url: str | None = None
    description: str = ""
    created_at: datetime = Field(default_factory=datetime.utcnow)

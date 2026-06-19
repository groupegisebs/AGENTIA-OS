from datetime import datetime
from uuid import uuid4

from pydantic import BaseModel, Field

from agent_creator.models.subscription import SubscriptionPlan


class Organization(BaseModel):
    """Organisation cliente (tenant) avec abonnement et limites."""

    id: str = Field(default_factory=lambda: str(uuid4()))
    name: str
    plan: SubscriptionPlan = SubscriptionPlan.FREE
    billing_email: str | None = None
    stripe_customer_id: str | None = None
    created_at: datetime = Field(default_factory=datetime.utcnow)
    updated_at: datetime = Field(default_factory=datetime.utcnow)

    def touch(self) -> None:
        self.updated_at = datetime.utcnow()

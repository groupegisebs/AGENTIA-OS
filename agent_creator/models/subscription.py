from enum import Enum

from pydantic import BaseModel, Field


class SubscriptionPlan(str, Enum):
    FREE = "free"
    PROFESSIONAL = "professional"
    BUSINESS = "business"
    ENTERPRISE = "enterprise"


class PlanFeatures(BaseModel):
    blueprint_generation: bool = True
    priority_support: bool = False
    multi_department: bool = False
    sso: bool = False
    custom_integrations: bool = False
    deployment_override: bool = False


class PlanLimits(BaseModel):
    max_deployments_per_month: int = Field(..., ge=0, description="0 = illimité")
    deployment_base_fee_eur: float = Field(..., ge=0)
    max_conversations_per_month: int = Field(default=0, ge=0, description="0 = illimité")
    max_team_members: int = Field(default=1, ge=0, description="0 = illimité")


class SubscriptionPlanConfig(BaseModel):
    plan: SubscriptionPlan
    name: str
    description: str
    limits: PlanLimits
    features: PlanFeatures
    monthly_price_eur: float = Field(..., ge=0)



from datetime import datetime

from pydantic import BaseModel

from agent_creator.models.billing import BillingEvent, BillingEventStatus, BillingEventType
from agent_creator.models.deployment import Deployment, DeploymentStatus
from agent_creator.models.organization import Organization
from agent_creator.models.subscription import PlanFeatures, PlanLimits, SubscriptionPlan, SubscriptionPlanConfig


class PlanFeaturesResponse(BaseModel):
    blueprint_generation: bool
    priority_support: bool
    multi_department: bool
    sso: bool
    custom_integrations: bool
    deployment_override: bool

    @classmethod
    def from_features(cls, features: PlanFeatures) -> "PlanFeaturesResponse":
        return cls(
            blueprint_generation=features.blueprint_generation,
            priority_support=features.priority_support,
            multi_department=features.multi_department,
            sso=features.sso,
            custom_integrations=features.custom_integrations,
            deployment_override=features.deployment_override,
        )


class PlanLimitsResponse(BaseModel):
    max_deployments_per_month: int | None
    deployment_base_fee_eur: float
    max_conversations_per_month: int | None
    max_team_members: int | None

    @classmethod
    def from_limits(cls, limits: PlanLimits) -> "PlanLimitsResponse":
        return cls(
            max_deployments_per_month=limits.max_deployments_per_month or None,
            deployment_base_fee_eur=limits.deployment_base_fee_eur,
            max_conversations_per_month=limits.max_conversations_per_month or None,
            max_team_members=limits.max_team_members or None,
        )


class PlanResponse(BaseModel):
    plan: SubscriptionPlan
    name: str
    description: str
    monthly_price_eur: float
    limits: PlanLimitsResponse
    features: PlanFeaturesResponse

    @classmethod
    def from_config(cls, config: SubscriptionPlanConfig) -> "PlanResponse":
        return cls(
            plan=config.plan,
            name=config.name,
            description=config.description,
            monthly_price_eur=config.monthly_price_eur,
            limits=PlanLimitsResponse.from_limits(config.limits),
            features=PlanFeaturesResponse.from_features(config.features),
        )


class OrganizationResponse(BaseModel):
    id: str
    name: str
    plan: SubscriptionPlan
    billing_email: str | None
    created_at: datetime

    @classmethod
    def from_organization(cls, org: Organization) -> "OrganizationResponse":
        return cls(
            id=org.id,
            name=org.name,
            plan=org.plan,
            billing_email=org.billing_email,
            created_at=org.created_at,
        )


class OrganizationDetailResponse(OrganizationResponse):
    plan_name: str
    deployments_used_this_month: int
    deployments_limit: int | None
    monthly_subscription_eur: float


class DeploymentResponse(BaseModel):
    id: str
    organization_id: str
    conversation_id: str
    blueprint_id: str
    status: DeploymentStatus
    deployment_cost: float
    currency: str
    complexity_score: float
    billed_at: datetime | None
    billing_event_id: str | None
    error_message: str | None
    created_at: datetime

    @classmethod
    def from_deployment(cls, deployment: Deployment) -> "DeploymentResponse":
        return cls(
            id=deployment.id,
            organization_id=deployment.organization_id,
            conversation_id=deployment.conversation_id,
            blueprint_id=deployment.blueprint_id,
            status=deployment.status,
            deployment_cost=deployment.deployment_cost,
            currency=deployment.currency,
            complexity_score=deployment.complexity_score,
            billed_at=deployment.billed_at,
            billing_event_id=deployment.billing_event_id,
            error_message=deployment.error_message,
            created_at=deployment.created_at,
        )


class BillingEventResponse(BaseModel):
    id: str
    event_type: BillingEventType
    amount: float
    currency: str
    status: BillingEventStatus
    deployment_id: str | None
    description: str
    created_at: datetime
    payment_provider_charge_id: str | None = None
    checkout_url: str | None = None

    @classmethod
    def from_event(cls, event: BillingEvent) -> "BillingEventResponse":
        return cls(
            id=event.id,
            event_type=event.event_type,
            amount=event.amount,
            currency=event.currency,
            status=event.status,
            deployment_id=event.deployment_id,
            description=event.description,
            created_at=event.created_at,
            payment_provider_charge_id=event.payment_provider_charge_id,
            checkout_url=event.checkout_url,
        )


class DeployResponse(BaseModel):
    deployment: DeploymentResponse
    billing_event: BillingEventResponse | None = None
    message: str
    checkout_url: str | None = None
    payment_code: str | None = None
    payment_pending: bool = False


class SubscribeRequest(BaseModel):
    plan: SubscriptionPlan


class SubscribeResponse(BaseModel):
    plan: SubscriptionPlan
    success: bool
    message: str
    checkout_url: str | None = None
    payment_code: str | None = None
    client_secret: str | None = None
    publishable_key: str | None = None


class ConfirmPaymentRequest(BaseModel):
    payment_code: str


class BillingSummaryResponse(BaseModel):
    organization: OrganizationResponse
    plan_name: str
    currency: str = "EUR"
    deployments_used_this_month: int
    deployments_limit: int | None
    total_billed: float
    deployments: list[DeploymentResponse]
    billing_events: list[BillingEventResponse]

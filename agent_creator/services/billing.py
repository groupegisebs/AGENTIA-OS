from datetime import datetime

from agent_creator.config import Settings
from agent_creator.models.billing import BillingEvent, BillingEventStatus, BillingEventType
from agent_creator.models.blueprint import Blueprint
from agent_creator.models.deployment import Deployment, DeploymentStatus
from agent_creator.models.organization import Organization
from agent_creator.models.subscription import SubscriptionPlan
from agent_creator.services.payment import PaymentProvider, SubscriptionCheckoutResult
from agent_creator.services.plans import get_plan_config


class DeploymentLimitExceeded(Exception):
    def __init__(self, message: str, deployments_used: int, limit: int) -> None:
        super().__init__(message)
        self.deployments_used = deployments_used
        self.limit = limit


class BillingService:
    """Calcul des coûts, vérification des limites et facturation au déploiement."""

    CURRENCY = "EUR"

    def __init__(self, settings: Settings, payment_provider: PaymentProvider) -> None:
        self._settings = settings
        self._payment = payment_provider

    def calculate_complexity_score(self, blueprint: Blueprint) -> float:
        component_count = len(blueprint.components)
        base = 1.0
        if component_count > 3:
            base += (component_count - 3) * self._settings.deployment_complexity_multiplier
        if blueprint.solution_type.value in ("hybrid", "microservice"):
            base += 0.2
        return round(base, 2)

    def calculate_deployment_cost(self, organization: Organization, blueprint: Blueprint) -> float:
        plan_config = get_plan_config(organization.plan)
        base_fee = plan_config.limits.deployment_base_fee_eur
        env_base = self._settings.deployment_base_fee
        if env_base > 0:
            base_fee = env_base

        complexity = self.calculate_complexity_score(blueprint)
        cost = round(base_fee * complexity, 2)
        return cost

    def count_deployments_this_month(
        self,
        organization_id: str,
        deployments: list[Deployment],
    ) -> int:
        now = datetime.utcnow()
        return sum(
            1
            for d in deployments
            if d.organization_id == organization_id
            and d.status == DeploymentStatus.DEPLOYED
            and d.billed_at
            and d.billed_at.year == now.year
            and d.billed_at.month == now.month
        )

    def check_deployment_allowed(
        self,
        organization: Organization,
        deployments: list[Deployment],
    ) -> None:
        plan_config = get_plan_config(organization.plan)
        limit = plan_config.limits.max_deployments_per_month

        if limit == 0 or plan_config.features.deployment_override:
            return

        used = self.count_deployments_this_month(organization.id, deployments)
        if used >= limit:
            raise DeploymentLimitExceeded(
                f"Limite de déploiements atteinte pour le plan {plan_config.name} "
                f"({used}/{limit} ce mois-ci). Passez à un plan supérieur.",
                deployments_used=used,
                limit=limit,
            )

    async def charge_deployment(
        self,
        organization: Organization,
        deployment: Deployment,
        blueprint_title: str,
    ) -> BillingEvent:
        event = BillingEvent(
            organization_id=organization.id,
            event_type=BillingEventType.DEPLOYMENT_CHARGE,
            amount=deployment.deployment_cost,
            currency=self.CURRENCY,
            deployment_id=deployment.id,
            description=f"Déploiement agent : {blueprint_title}",
        )

        result = await self._payment.create_charge(
            amount=deployment.deployment_cost,
            currency=self.CURRENCY,
            customer_id=organization.stripe_customer_id or f"AF-{organization.id}",
            description=event.description,
            metadata={
                "deployment_id": deployment.id,
                "organization_id": organization.id,
                "conversation_id": deployment.conversation_id,
            },
            customer_email=organization.billing_email,
            organization_id=organization.id,
        )

        if result.success:
            event.status = BillingEventStatus.SUCCEEDED
            event.payment_provider_charge_id = result.charge_id
            deployment.status = DeploymentStatus.DEPLOYED
            deployment.billed_at = datetime.utcnow()
            deployment.billing_event_id = event.id
        elif result.pending:
            event.status = BillingEventStatus.PENDING
            event.payment_provider_charge_id = result.payment_code or result.charge_id
            event.checkout_url = result.checkout_url
            deployment.status = DeploymentStatus.BILLING
            deployment.billing_event_id = event.id
            deployment.error_message = result.error_message
        else:
            event.status = BillingEventStatus.FAILED
            deployment.status = DeploymentStatus.FAILED
            deployment.error_message = result.error_message

        return event

    async def confirm_deployment_payment(
        self,
        deployment: Deployment,
        billing_event: BillingEvent,
        payment_code: str,
    ) -> BillingEvent:
        """Finalise un déploiement dont le paiement était en attente (GiseBsPayGateway)."""
        result = await self._payment.confirm_payment(payment_code)
        if not result.success:
            billing_event.status = BillingEventStatus.PENDING
            deployment.error_message = result.error_message
            return billing_event

        billing_event.status = BillingEventStatus.SUCCEEDED
        billing_event.payment_provider_charge_id = payment_code
        deployment.status = DeploymentStatus.DEPLOYED
        deployment.billed_at = datetime.utcnow()
        deployment.error_message = None
        return billing_event

    async def create_subscription_checkout(
        self,
        organization: Organization,
        target_plan: SubscriptionPlan,
    ) -> SubscriptionCheckoutResult:
        if target_plan == SubscriptionPlan.FREE:
            organization.plan = SubscriptionPlan.FREE
            organization.touch()
            return SubscriptionCheckoutResult(success=True)

        email = organization.billing_email or f"{organization.id}@agentia.factory"
        customer_code = organization.stripe_customer_id or f"AF-{organization.id}"

        return await self._payment.create_subscription_checkout(
            organization_id=organization.id,
            customer_code=customer_code,
            email=email,
            plan=target_plan,
            full_name=organization.name,
        )

    def estimate_deployment_cost_message(self, organization: Organization, blueprint: Blueprint) -> str:
        cost = self.calculate_deployment_cost(organization, blueprint)
        plan_name = get_plan_config(organization.plan).name
        return (
            f"Le déploiement de cet agent coûtera environ {cost:.2f} {self.CURRENCY} "
            f"(plan {plan_name}). La génération du blueprint reste gratuite."
        )

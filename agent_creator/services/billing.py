from datetime import datetime

from agent_creator.config import Settings
from agent_creator.db.repository import DbStore, PaymentIntent, PaymentIntentStatus, PaymentIntentType, new_payment_intent
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

    def resolve_deploy_plan_code(self, complexity_score: float) -> str:
        if complexity_score <= 1.15:
            return self._settings.gisebs_pay_deploy_plan_small
        if complexity_score <= 1.45:
            return self._settings.gisebs_pay_deploy_plan_medium
        return self._settings.gisebs_pay_deploy_plan_large

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
        db: DbStore,
        organization: Organization,
        deployment: Deployment,
        blueprint: Blueprint,
    ) -> tuple[BillingEvent, PaymentIntent | None]:
        event = BillingEvent(
            organization_id=organization.id,
            event_type=BillingEventType.DEPLOYMENT_CHARGE,
            amount=deployment.deployment_cost,
            currency=self.CURRENCY,
            deployment_id=deployment.id,
            description=f"Déploiement solution : {blueprint.title}",
        )

        plan_code = self.resolve_deploy_plan_code(deployment.complexity_score)

        result = await self._payment.create_charge(
            amount=deployment.deployment_cost,
            currency=self.CURRENCY,
            customer_id=organization.stripe_customer_id or f"AF-{organization.id}",
            description=event.description,
            metadata={
                "deployment_id": deployment.id,
                "organization_id": organization.id,
                "conversation_id": deployment.conversation_id,
                "complexity_score": deployment.complexity_score,
                "deploy_plan_code": plan_code,
            },
            customer_email=organization.billing_email,
            organization_id=organization.id,
            deploy_plan_code=plan_code,
        )

        payment_intent: PaymentIntent | None = None

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
            payment_intent = new_payment_intent(
                organization_id=organization.id,
                intent_type=PaymentIntentType.DEPLOY,
                status=PaymentIntentStatus.PENDING,
                payment_code=result.payment_code,
                amount=deployment.deployment_cost,
                currency=self.CURRENCY,
                conversation_id=deployment.conversation_id,
                deployment_id=deployment.id,
                billing_event_id=event.id,
            )
        else:
            event.status = BillingEventStatus.FAILED
            deployment.status = DeploymentStatus.FAILED
            deployment.error_message = result.error_message

        return event, payment_intent

    async def confirm_deployment_payment(
        self,
        db: DbStore,
        deployment: Deployment,
        billing_event: BillingEvent,
        payment_code: str,
    ) -> BillingEvent:
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

        intent = await db.get_payment_intent_by_code(payment_code)
        if intent:
            intent.status = PaymentIntentStatus.SUCCEEDED
            await db.save_payment_intent(intent)

        return billing_event

    async def create_subscription_checkout(
        self,
        db: DbStore,
        organization: Organization,
        target_plan: SubscriptionPlan,
    ) -> tuple[SubscriptionCheckoutResult, PaymentIntent | None]:
        if target_plan == SubscriptionPlan.FREE:
            organization.plan = SubscriptionPlan.FREE
            organization.touch()
            await db.save_organization(organization)
            return SubscriptionCheckoutResult(success=True), None

        email = organization.billing_email or f"{organization.id}@agentia.factory"
        customer_code = organization.stripe_customer_id or f"AF-{organization.id}"

        result = await self._payment.create_subscription_checkout(
            organization_id=organization.id,
            customer_code=customer_code,
            email=email,
            plan=target_plan,
            full_name=organization.name,
        )

        payment_intent = None
        if result.success and result.payment_code:
            plan_config = get_plan_config(target_plan)
            payment_intent = new_payment_intent(
                organization_id=organization.id,
                intent_type=PaymentIntentType.SUBSCRIBE,
                status=PaymentIntentStatus.PENDING,
                payment_code=result.payment_code,
                amount=plan_config.monthly_price_eur,
                currency=self.CURRENCY,
                target_plan=target_plan.value,
            )

        return result, payment_intent

    async def confirm_subscription_payment(
        self,
        db: DbStore,
        organization: Organization,
        payment_code: str,
        target_plan: SubscriptionPlan | None = None,
    ) -> Organization:
        intent = await db.get_payment_intent_by_code(payment_code)
        if intent and intent.organization_id != organization.id:
            raise ValueError("Paiement non associé à cette organisation")

        plan = target_plan
        if intent and intent.target_plan:
            plan = SubscriptionPlan(intent.target_plan)
        if not plan:
            raise ValueError("Plan cible introuvable pour cette confirmation")

        result = await self._payment.confirm_payment(payment_code)
        if not result.success:
            raise ValueError(result.error_message or "Paiement non finalisé")

        organization.plan = plan
        organization.touch()
        await db.save_organization(organization)

        event = BillingEvent(
            organization_id=organization.id,
            event_type=BillingEventType.SUBSCRIPTION,
            status=BillingEventStatus.SUCCEEDED,
            amount=get_plan_config(plan).monthly_price_eur,
            currency=self.CURRENCY,
            description=f"Abonnement {get_plan_config(plan).name}",
            payment_provider_charge_id=payment_code,
        )
        await db.save_billing_event(event)

        if intent:
            intent.status = PaymentIntentStatus.SUCCEEDED
            await db.save_payment_intent(intent)

        return organization

    async def confirm_payment_unified(
        self,
        db: DbStore,
        organization: Organization,
        payment_code: str,
    ) -> dict:
        intent = await db.get_payment_intent_by_code(payment_code)
        if not intent or intent.organization_id != organization.id:
            raise ValueError("Intention de paiement introuvable")

        if intent.intent_type == PaymentIntentType.SUBSCRIBE:
            org = await self.confirm_subscription_payment(
                db, organization, payment_code,
                SubscriptionPlan(intent.target_plan) if intent.target_plan else None,
            )
            return {"type": "subscribe", "organization": org, "success": True}

        if intent.intent_type == PaymentIntentType.DEPLOY and intent.conversation_id:
            deployments = await db.list_deployments_for_conversation(intent.conversation_id)
            pending = next((d for d in deployments if d.status == DeploymentStatus.BILLING), None)
            if not pending:
                raise ValueError("Déploiement en attente introuvable")
            billing_event = await db.get_billing_event(pending.billing_event_id or "")
            if not billing_event:
                raise ValueError("Événement de facturation introuvable")
            billing_event = await self.confirm_deployment_payment(db, pending, billing_event, payment_code)
            await db.save_billing_event(billing_event)
            await db.save_deployment(pending)
            return {"type": "deploy", "deployment": pending, "billing_event": billing_event, "success": True}

        raise ValueError("Type de paiement non pris en charge")

    def estimate_deployment_cost_message(self, organization: Organization, blueprint: Blueprint) -> str:
        cost = self.calculate_deployment_cost(organization, blueprint)
        plan_name = get_plan_config(organization.plan).name
        return (
            f"Le déploiement de cette solution coûtera environ {cost:.2f} {self.CURRENCY} "
            f"(plan {plan_name}). La conception reste gratuite."
        )

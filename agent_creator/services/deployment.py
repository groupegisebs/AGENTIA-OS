from agent_creator.config import get_settings
from agent_creator.db.repository import DbStore
from agent_creator.models.billing import BillingEvent, BillingEventStatus
from agent_creator.models.deployment import Deployment, DeploymentStatus
from agent_creator.models.organization import Organization
from agent_creator.models.subscription import SubscriptionPlanConfig
from agent_creator.services.agent_publish import publish_from_deployment
from agent_creator.services.billing import BillingService
from agent_creator.services.plans import get_plan_config, list_plans


class DeploymentService:
    """Orchestre le déploiement d'un blueprint avec facturation."""

    def __init__(self, db: DbStore, billing_service: BillingService) -> None:
        self._db = db
        self._billing = billing_service

    def get_plans(self) -> list[SubscriptionPlanConfig]:
        return list_plans()

    async def get_organization(self, organization_id: str) -> Organization | None:
        return await self._db.get_organization(organization_id)

    async def deploy_blueprint(
        self,
        conversation_id: str,
        organization_id: str,
    ) -> tuple[Deployment, BillingEvent | None]:
        conversation = await self._db.get_conversation(conversation_id, organization_id)
        if not conversation:
            raise ValueError("Conversation introuvable")

        blueprint = await self._db.get_blueprint(conversation_id)
        if not blueprint:
            raise ValueError(
                "Aucun blueprint généré pour cette conversation. "
                "Appelez GET /conversations/{id}/blueprint d'abord (gratuit)."
            )

        organization = await self._db.get_organization(organization_id)
        if not organization:
            raise ValueError("Organisation introuvable")

        existing = await self._db.list_deployments_for_conversation(conversation_id)
        successful = [d for d in existing if d.status == DeploymentStatus.DEPLOYED]
        if successful:
            return successful[0], None

        all_deployments = await self._db.list_deployments(organization_id)
        self._billing.check_deployment_allowed(organization, all_deployments)

        cost = self._billing.calculate_deployment_cost(organization, blueprint)
        complexity = self._billing.calculate_complexity_score(blueprint)

        deployment = Deployment(
            organization_id=organization_id,
            conversation_id=conversation_id,
            blueprint_id=blueprint.id,
            status=DeploymentStatus.BILLING,
            deployment_cost=cost,
            currency=BillingService.CURRENCY,
            complexity_score=complexity,
        )
        await self._db.save_deployment(deployment)

        billing_event, payment_intent = await self._billing.charge_deployment(
            self._db, organization, deployment, blueprint
        )
        await self._db.save_billing_event(billing_event)
        await self._db.save_deployment(deployment)
        if payment_intent:
            await self._db.save_payment_intent(payment_intent)

        if deployment.status == DeploymentStatus.DEPLOYED:
            await publish_from_deployment(self._db, deployment, blueprint, organization, get_settings())

        return deployment, billing_event

    async def confirm_deployment_payment(
        self,
        conversation_id: str,
        organization_id: str,
        payment_code: str,
    ) -> tuple[Deployment, BillingEvent]:
        deployment = await self._find_pending_deployment(conversation_id, organization_id, payment_code)
        if not deployment:
            raise ValueError("Aucun déploiement en attente de paiement pour cette conversation.")

        billing_event = await self._db.get_billing_event(deployment.billing_event_id or "")
        if not billing_event:
            raise ValueError("Événement de facturation introuvable.")

        billing_event = await self._billing.confirm_deployment_payment(
            self._db, deployment, billing_event, payment_code
        )
        await self._db.save_billing_event(billing_event)
        await self._db.save_deployment(deployment)

        if deployment.status == DeploymentStatus.DEPLOYED:
            blueprint = await self._db.get_blueprint(deployment.conversation_id)
            organization = await self._db.get_organization(deployment.organization_id)
            if blueprint and organization:
                await publish_from_deployment(self._db, deployment, blueprint, organization, get_settings())

        return deployment, billing_event

    async def _find_pending_deployment(
        self,
        conversation_id: str,
        organization_id: str,
        payment_code: str,
    ) -> Deployment | None:
        for deployment in await self._db.list_deployments(organization_id):
            if deployment.conversation_id != conversation_id:
                continue
            if deployment.status != DeploymentStatus.BILLING:
                continue
            event = await self._db.get_billing_event(deployment.billing_event_id or "")
            if event and event.payment_provider_charge_id == payment_code:
                return deployment
        return None

    async def get_billing_summary(self, organization_id: str) -> dict:
        organization = await self._db.get_organization(organization_id)
        if not organization:
            raise ValueError("Organisation introuvable")

        plan_config = get_plan_config(organization.plan)
        deployments = await self._db.list_deployments(organization_id)
        events = await self._db.list_billing_events(organization_id)
        used = self._billing.count_deployments_this_month(organization_id, deployments)
        limit = plan_config.limits.max_deployments_per_month

        total_billed = sum(
            e.amount for e in events if e.status == BillingEventStatus.SUCCEEDED
        )

        return {
            "organization": organization,
            "plan_config": plan_config,
            "deployments_used_this_month": used,
            "deployments_limit": limit if limit > 0 else None,
            "total_billed_eur": round(total_billed, 2),
            "deployments": deployments,
            "billing_events": events,
        }

from agent_creator.models.billing import BillingEvent, BillingEventStatus
from agent_creator.models.deployment import Deployment, DeploymentStatus
from agent_creator.models.organization import Organization
from agent_creator.models.subscription import SubscriptionPlanConfig
from agent_creator.services.billing import BillingService
from agent_creator.services.organization_store import OrganizationStore
from agent_creator.services.plans import get_plan_config, list_plans
from agent_creator.services.store import ConversationStore


class DeploymentService:
    """Orchestre le déploiement d'un blueprint avec facturation."""

    def __init__(
        self,
        conversation_store: ConversationStore,
        organization_store: OrganizationStore,
        billing_service: BillingService,
    ) -> None:
        self._conversations = conversation_store
        self._organizations = organization_store
        self._billing = billing_service

    def get_plans(self) -> list[SubscriptionPlanConfig]:
        return list_plans()

    def get_organization(self, organization_id: str) -> Organization | None:
        return self._organizations.get(organization_id)

    async def deploy_blueprint(
        self,
        conversation_id: str,
        organization_id: str,
    ) -> tuple[Deployment, BillingEvent | None]:
        conversation = self._conversations.get(conversation_id)
        if not conversation:
            raise ValueError("Conversation introuvable")

        blueprint = self._conversations.get_blueprint(conversation_id)
        if not blueprint:
            raise ValueError(
                "Aucun blueprint généré pour cette conversation. "
                "Appelez GET /conversations/{id}/blueprint d'abord (gratuit)."
            )

        organization = self._organizations.get(organization_id)
        if not organization:
            raise ValueError("Organisation introuvable")

        existing = self._organizations.list_deployments_for_conversation(conversation_id)
        successful = [d for d in existing if d.status == DeploymentStatus.DEPLOYED]
        if successful:
            return successful[0], None

        all_deployments = self._organizations.list_deployments(organization_id)
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
        self._organizations.save_deployment(deployment)

        billing_event = await self._billing.charge_deployment(
            organization, deployment, blueprint.title
        )
        self._organizations.save_billing_event(billing_event)
        self._organizations.save_deployment(deployment)

        return deployment, billing_event

    async def confirm_deployment_payment(
        self,
        conversation_id: str,
        organization_id: str,
        payment_code: str,
    ) -> tuple[Deployment, BillingEvent]:
        deployment = self._find_pending_deployment(conversation_id, organization_id, payment_code)
        if not deployment:
            raise ValueError("Aucun déploiement en attente de paiement pour cette conversation.")

        billing_event = self._organizations.get_billing_event(deployment.billing_event_id or "")
        if not billing_event:
            raise ValueError("Événement de facturation introuvable.")

        billing_event = await self._billing.confirm_deployment_payment(
            deployment, billing_event, payment_code
        )
        self._organizations.save_billing_event(billing_event)
        self._organizations.save_deployment(deployment)
        return deployment, billing_event

    def _find_pending_deployment(
        self,
        conversation_id: str,
        organization_id: str,
        payment_code: str,
    ) -> Deployment | None:
        for deployment in self._organizations.list_deployments(organization_id):
            if deployment.conversation_id != conversation_id:
                continue
            if deployment.status != DeploymentStatus.BILLING:
                continue
            event = self._organizations.get_billing_event(deployment.billing_event_id or "")
            if event and event.payment_provider_charge_id == payment_code:
                return deployment
        return None

    def get_billing_summary(self, organization_id: str) -> dict:
        organization = self._organizations.get(organization_id)
        if not organization:
            raise ValueError("Organisation introuvable")

        plan_config = get_plan_config(organization.plan)
        deployments = self._organizations.list_deployments(organization_id)
        events = self._organizations.list_billing_events(organization_id)
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

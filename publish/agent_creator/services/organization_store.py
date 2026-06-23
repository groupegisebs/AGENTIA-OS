from agent_creator.models.billing import BillingEvent
from agent_creator.models.deployment import Deployment
from agent_creator.models.organization import Organization
from agent_creator.models.subscription import SubscriptionPlan


class OrganizationStore:
    """Stockage en mémoire des organisations, déploiements et événements de facturation."""

    def __init__(self, default_org_id: str, default_org_name: str = "Organisation démo") -> None:
        self._organizations: dict[str, Organization] = {}
        self._deployments: dict[str, Deployment] = {}
        self._billing_events: dict[str, BillingEvent] = {}
        self._default_org_id = default_org_id
        self._seed_default_org(default_org_id, default_org_name)

    def _seed_default_org(self, org_id: str, name: str) -> None:
        org = Organization(
            id=org_id,
            name=name,
            plan=SubscriptionPlan.FREE,
            billing_email="demo@agentia.factory",
        )
        self._organizations[org_id] = org

    @property
    def default_org_id(self) -> str:
        return self._default_org_id

    def get(self, organization_id: str) -> Organization | None:
        return self._organizations.get(organization_id)

    def get_default(self) -> Organization:
        return self._organizations[self._default_org_id]

    def save(self, organization: Organization) -> Organization:
        organization.touch()
        self._organizations[organization.id] = organization
        return organization

    def list_all(self) -> list[Organization]:
        return list(self._organizations.values())

    def save_deployment(self, deployment: Deployment) -> Deployment:
        self._deployments[deployment.id] = deployment
        return deployment

    def get_deployment(self, deployment_id: str) -> Deployment | None:
        return self._deployments.get(deployment_id)

    def list_deployments(self, organization_id: str | None = None) -> list[Deployment]:
        deployments = list(self._deployments.values())
        if organization_id:
            deployments = [d for d in deployments if d.organization_id == organization_id]
        return sorted(deployments, key=lambda d: d.created_at, reverse=True)

    def list_deployments_for_conversation(self, conversation_id: str) -> list[Deployment]:
        return [d for d in self._deployments.values() if d.conversation_id == conversation_id]

    def save_billing_event(self, event: BillingEvent) -> BillingEvent:
        self._billing_events[event.id] = event
        return event

    def get_billing_event(self, event_id: str) -> BillingEvent | None:
        return self._billing_events.get(event_id)

    def list_billing_events(self, organization_id: str) -> list[BillingEvent]:
        events = [e for e in self._billing_events.values() if e.organization_id == organization_id]
        return sorted(events, key=lambda e: e.created_at, reverse=True)

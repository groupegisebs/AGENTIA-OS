import json
from datetime import datetime
from uuid import uuid4

from sqlalchemy import select
from sqlalchemy.ext.asyncio import AsyncSession
from sqlalchemy.orm import selectinload

from agent_creator.db.tables import (
    AgentApiKeyRow,
    AgentInvocationRow,
    BillingEventRow,
    BlueprintRow,
    ConversationRow,
    DeploymentRow,
    MessageRow,
    OrganizationRow,
    PaymentIntentRow,
    PublishedAgentRow,
)
from agent_creator.models.agent import AgentApiKey, AgentInvocation, AgentManifest, AgentStatus, AgentVisibility, PublishedAgent
from agent_creator.models.billing import BillingEvent, BillingEventStatus, BillingEventType
from agent_creator.models.blueprint import Blueprint
from agent_creator.models.conversation import Conversation, ConversationStatus, Message, MessageRole
from agent_creator.models.deployment import Deployment, DeploymentStatus
from agent_creator.models.organization import Organization
from agent_creator.models.subscription import SubscriptionPlan


class PaymentIntentType:
    DEPLOY = "deploy"
    SUBSCRIBE = "subscribe"


class PaymentIntentStatus:
    PENDING = "pending"
    SUCCEEDED = "succeeded"
    FAILED = "failed"
    CANCELLED = "cancelled"


class PaymentIntent:
    def __init__(
        self,
        *,
        id: str,
        organization_id: str,
        intent_type: str,
        status: str,
        payment_code: str | None = None,
        amount: float = 0.0,
        currency: str = "EUR",
        conversation_id: str | None = None,
        target_plan: str | None = None,
        deployment_id: str | None = None,
        billing_event_id: str | None = None,
        created_at: datetime | None = None,
    ) -> None:
        self.id = id
        self.organization_id = organization_id
        self.intent_type = intent_type
        self.status = status
        self.payment_code = payment_code
        self.amount = amount
        self.currency = currency
        self.conversation_id = conversation_id
        self.target_plan = target_plan
        self.deployment_id = deployment_id
        self.billing_event_id = billing_event_id
        self.created_at = created_at or datetime.utcnow()


def _org_from_row(row: OrganizationRow) -> Organization:
    return Organization(
        id=row.id,
        name=row.name,
        plan=SubscriptionPlan(row.plan),
        billing_email=row.billing_email,
        stripe_customer_id=row.gisebs_customer_code,
        created_at=row.created_at,
        updated_at=row.updated_at,
    )


def _conversation_from_row(row: ConversationRow) -> Conversation:
    questions = json.loads(row.clarifying_questions_json or "[]")
    messages = [
        Message(id=m.id, role=MessageRole(m.role), content=m.content, created_at=m.created_at)
        for m in sorted(row.messages, key=lambda x: x.created_at)
    ]
    return Conversation(
        id=row.id,
        organization_id=row.organization_id,
        title=row.title,
        status=ConversationStatus(row.status),
        messages=messages,
        clarifying_questions=questions,
        created_at=row.created_at,
        updated_at=row.updated_at,
    )


class DbStore:
    """Persistance PostgreSQL/SQLite async pour conversations, orgs et facturation."""

    def __init__(self, session: AsyncSession) -> None:
        self._session = session

    # --- Organizations ---

    async def get_organization(self, organization_id: str) -> Organization | None:
        row = await self._session.get(OrganizationRow, organization_id)
        return _org_from_row(row) if row else None

    async def save_organization(self, organization: Organization) -> Organization:
        row = await self._session.get(OrganizationRow, organization.id)
        if not row:
            row = OrganizationRow(id=organization.id)
            self._session.add(row)
        row.name = organization.name
        row.plan = organization.plan.value
        row.billing_email = organization.billing_email
        row.gisebs_customer_code = organization.stripe_customer_id
        row.updated_at = datetime.utcnow()
        await self._session.flush()
        return organization

    # --- Conversations ---

    async def create_conversation(self, conversation: Conversation) -> Conversation:
        row = ConversationRow(
            id=conversation.id,
            organization_id=conversation.organization_id,
            title=conversation.title,
            status=conversation.status.value,
            clarifying_questions_json=json.dumps(conversation.clarifying_questions, ensure_ascii=False),
            created_at=conversation.created_at,
            updated_at=conversation.updated_at,
        )
        self._session.add(row)
        for msg in conversation.messages:
            self._session.add(
                MessageRow(
                    id=msg.id,
                    conversation_id=conversation.id,
                    role=msg.role.value,
                    content=msg.content,
                    created_at=msg.created_at,
                )
            )
        await self._session.flush()
        return conversation

    async def get_conversation(self, conversation_id: str, organization_id: str | None = None) -> Conversation | None:
        stmt = (
            select(ConversationRow)
            .where(ConversationRow.id == conversation_id)
            .options(selectinload(ConversationRow.messages))
        )
        if organization_id:
            stmt = stmt.where(ConversationRow.organization_id == organization_id)
        row = await self._session.scalar(stmt)
        return _conversation_from_row(row) if row else None

    async def save_conversation(self, conversation: Conversation) -> Conversation:
        row = await self._session.get(
            ConversationRow,
            conversation.id,
            options=[selectinload(ConversationRow.messages)],
        )
        if not row:
            return await self.create_conversation(conversation)
        row.title = conversation.title
        row.status = conversation.status.value
        row.clarifying_questions_json = json.dumps(conversation.clarifying_questions, ensure_ascii=False)
        row.updated_at = conversation.updated_at
        existing_ids = {m.id for m in row.messages}
        for msg in conversation.messages:
            if msg.id not in existing_ids:
                self._session.add(
                    MessageRow(
                        id=msg.id,
                        conversation_id=conversation.id,
                        role=msg.role.value,
                        content=msg.content,
                        created_at=msg.created_at,
                    )
                )
        await self._session.flush()
        return conversation

    async def list_conversations(self, organization_id: str) -> list[Conversation]:
        stmt = (
            select(ConversationRow)
            .where(ConversationRow.organization_id == organization_id)
            .options(selectinload(ConversationRow.messages))
            .order_by(ConversationRow.updated_at.desc())
        )
        rows = (await self._session.scalars(stmt)).all()
        return [_conversation_from_row(r) for r in rows]

    async def save_blueprint(self, blueprint: Blueprint) -> Blueprint:
        row = await self._session.scalar(
            select(BlueprintRow).where(BlueprintRow.conversation_id == blueprint.conversation_id)
        )
        data = blueprint.model_dump_json()
        if row:
            row.data_json = data
        else:
            self._session.add(
                BlueprintRow(id=blueprint.id, conversation_id=blueprint.conversation_id, data_json=data)
            )
        await self._session.flush()
        return blueprint

    async def get_blueprint(self, conversation_id: str) -> Blueprint | None:
        row = await self._session.scalar(
            select(BlueprintRow).where(BlueprintRow.conversation_id == conversation_id)
        )
        if not row:
            return None
        return Blueprint.model_validate_json(row.data_json)

    # --- Deployments & billing ---

    async def save_deployment(self, deployment: Deployment) -> Deployment:
        row = await self._session.get(DeploymentRow, deployment.id)
        if not row:
            row = DeploymentRow(id=deployment.id)
            self._session.add(row)
        row.organization_id = deployment.organization_id
        row.conversation_id = deployment.conversation_id
        row.blueprint_id = deployment.blueprint_id
        row.status = deployment.status.value
        row.deployment_cost = deployment.deployment_cost
        row.currency = deployment.currency
        row.complexity_score = deployment.complexity_score
        row.billing_event_id = deployment.billing_event_id
        row.error_message = deployment.error_message
        row.billed_at = deployment.billed_at
        await self._session.flush()
        return deployment

    async def get_deployment(self, deployment_id: str) -> Deployment | None:
        row = await self._session.get(DeploymentRow, deployment_id)
        return _deployment_from_row(row) if row else None

    async def list_deployments(self, organization_id: str) -> list[Deployment]:
        stmt = (
            select(DeploymentRow)
            .where(DeploymentRow.organization_id == organization_id)
            .order_by(DeploymentRow.created_at.desc())
        )
        rows = (await self._session.scalars(stmt)).all()
        return [_deployment_from_row(r) for r in rows]

    async def list_deployments_for_conversation(self, conversation_id: str) -> list[Deployment]:
        stmt = select(DeploymentRow).where(DeploymentRow.conversation_id == conversation_id)
        rows = (await self._session.scalars(stmt)).all()
        return [_deployment_from_row(r) for r in rows]

    async def save_billing_event(self, event: BillingEvent) -> BillingEvent:
        row = await self._session.get(BillingEventRow, event.id)
        if not row:
            row = BillingEventRow(id=event.id)
            self._session.add(row)
        row.organization_id = event.organization_id
        row.event_type = event.event_type.value
        row.status = event.status.value
        row.amount = event.amount
        row.currency = event.currency
        row.deployment_id = event.deployment_id
        row.description = event.description
        row.payment_provider_charge_id = event.payment_provider_charge_id
        row.checkout_url = event.checkout_url
        await self._session.flush()
        return event

    async def get_billing_event(self, event_id: str) -> BillingEvent | None:
        row = await self._session.get(BillingEventRow, event_id)
        return _billing_from_row(row) if row else None

    async def list_billing_events(self, organization_id: str) -> list[BillingEvent]:
        stmt = (
            select(BillingEventRow)
            .where(BillingEventRow.organization_id == organization_id)
            .order_by(BillingEventRow.created_at.desc())
        )
        rows = (await self._session.scalars(stmt)).all()
        return [_billing_from_row(r) for r in rows]

    # --- Payment intents ---

    async def save_payment_intent(self, intent: PaymentIntent) -> PaymentIntent:
        row = await self._session.get(PaymentIntentRow, intent.id)
        if not row:
            row = PaymentIntentRow(id=intent.id)
            self._session.add(row)
        row.organization_id = intent.organization_id
        row.intent_type = intent.intent_type
        row.status = intent.status
        row.payment_code = intent.payment_code
        row.amount = intent.amount
        row.currency = intent.currency
        row.conversation_id = intent.conversation_id
        row.target_plan = intent.target_plan
        row.deployment_id = intent.deployment_id
        row.billing_event_id = intent.billing_event_id
        row.updated_at = datetime.utcnow()
        await self._session.flush()
        return intent

    async def get_payment_intent_by_code(self, payment_code: str) -> PaymentIntent | None:
        row = await self._session.scalar(
            select(PaymentIntentRow).where(PaymentIntentRow.payment_code == payment_code)
        )
        return _intent_from_row(row) if row else None

    # --- Published agents ---

    async def save_published_agent(self, agent: PublishedAgent) -> PublishedAgent:
        row = await self._session.get(PublishedAgentRow, agent.id)
        if not row:
            row = PublishedAgentRow(id=agent.id)
            self._session.add(row)
        row.organization_id = agent.organization_id
        row.deployment_id = agent.deployment_id
        row.blueprint_id = agent.blueprint_id
        row.title = agent.title
        row.description = agent.description
        row.category = agent.category
        row.visibility = agent.visibility.value
        row.status = agent.status.value
        row.manifest_json = agent.manifest.model_dump_json()
        row.updated_at = datetime.utcnow()
        await self._session.flush()
        return agent

    async def get_published_agent(self, agent_id: str) -> PublishedAgent | None:
        row = await self._session.get(PublishedAgentRow, agent_id)
        return _agent_from_row(row) if row else None

    async def get_published_agent_by_deployment(self, deployment_id: str) -> PublishedAgent | None:
        row = await self._session.scalar(
            select(PublishedAgentRow).where(PublishedAgentRow.deployment_id == deployment_id)
        )
        return _agent_from_row(row) if row else None

    async def list_published_agents(self, organization_id: str) -> list[PublishedAgent]:
        stmt = (
            select(PublishedAgentRow)
            .where(PublishedAgentRow.organization_id == organization_id)
            .order_by(PublishedAgentRow.created_at.desc())
        )
        rows = (await self._session.scalars(stmt)).all()
        return [_agent_from_row(r) for r in rows]

    async def list_public_agents(self) -> list[PublishedAgent]:
        stmt = (
            select(PublishedAgentRow)
            .where(
                PublishedAgentRow.visibility == AgentVisibility.PUBLIC.value,
                PublishedAgentRow.status == AgentStatus.ACTIVE.value,
            )
            .order_by(PublishedAgentRow.created_at.desc())
        )
        rows = (await self._session.scalars(stmt)).all()
        return [_agent_from_row(r) for r in rows]

    # --- Agent API keys ---

    async def save_agent_api_key(self, key: AgentApiKey) -> AgentApiKey:
        row = await self._session.get(AgentApiKeyRow, key.id)
        if not row:
            row = AgentApiKeyRow(id=key.id)
            self._session.add(row)
        row.agent_id = key.agent_id
        row.label = key.label
        row.key_hash = key.key_hash
        row.revoked_at = key.revoked_at
        await self._session.flush()
        return key

    async def get_agent_api_key_by_hash(self, key_hash: str) -> AgentApiKey | None:
        row = await self._session.scalar(
            select(AgentApiKeyRow).where(AgentApiKeyRow.key_hash == key_hash)
        )
        return _api_key_from_row(row) if row else None

    async def list_agent_api_keys(self, agent_id: str) -> list[AgentApiKey]:
        stmt = select(AgentApiKeyRow).where(AgentApiKeyRow.agent_id == agent_id)
        rows = (await self._session.scalars(stmt)).all()
        return [_api_key_from_row(r) for r in rows]

    # --- Agent invocations ---

    async def save_agent_invocation(self, inv: AgentInvocation) -> AgentInvocation:
        row = AgentInvocationRow(
            id=inv.id,
            agent_id=inv.agent_id,
            organization_id=inv.organization_id,
            input_chars=inv.input_chars,
            latency_ms=inv.latency_ms,
            status=inv.status,
            created_at=inv.created_at,
        )
        self._session.add(row)
        await self._session.flush()
        return inv

    async def count_recent_invocations(self, agent_id: str, organization_id: str, since: datetime) -> int:
        from sqlalchemy import func
        stmt = (
            select(func.count())
            .select_from(AgentInvocationRow)
            .where(
                AgentInvocationRow.agent_id == agent_id,
                AgentInvocationRow.organization_id == organization_id,
                AgentInvocationRow.created_at >= since,
            )
        )
        result = await self._session.scalar(stmt)
        return result or 0


def _deployment_from_row(row: DeploymentRow) -> Deployment:
    return Deployment(
        id=row.id,
        organization_id=row.organization_id,
        conversation_id=row.conversation_id,
        blueprint_id=row.blueprint_id,
        status=DeploymentStatus(row.status),
        deployment_cost=row.deployment_cost,
        currency=row.currency,
        complexity_score=row.complexity_score,
        billing_event_id=row.billing_event_id,
        error_message=row.error_message,
        billed_at=row.billed_at,
        created_at=row.created_at,
    )


def _billing_from_row(row: BillingEventRow) -> BillingEvent:
    return BillingEvent(
        id=row.id,
        organization_id=row.organization_id,
        event_type=BillingEventType(row.event_type),
        status=BillingEventStatus(row.status),
        amount=row.amount,
        currency=row.currency,
        deployment_id=row.deployment_id,
        description=row.description,
        payment_provider_charge_id=row.payment_provider_charge_id,
        checkout_url=row.checkout_url,
        created_at=row.created_at,
    )


def _intent_from_row(row: PaymentIntentRow) -> PaymentIntent:
    return PaymentIntent(
        id=row.id,
        organization_id=row.organization_id,
        intent_type=row.intent_type,
        status=row.status,
        payment_code=row.payment_code,
        amount=row.amount,
        currency=row.currency,
        conversation_id=row.conversation_id,
        target_plan=row.target_plan,
        deployment_id=row.deployment_id,
        billing_event_id=row.billing_event_id,
        created_at=row.created_at,
    )


def new_payment_intent(**kwargs) -> PaymentIntent:
    return PaymentIntent(id=str(uuid4()), **kwargs)


def _agent_from_row(row: PublishedAgentRow) -> PublishedAgent:
    return PublishedAgent(
        id=row.id,
        organization_id=row.organization_id,
        deployment_id=row.deployment_id,
        blueprint_id=row.blueprint_id,
        title=row.title,
        description=row.description or "",
        category=row.category or "Général",
        visibility=AgentVisibility(row.visibility),
        status=AgentStatus(row.status),
        manifest=AgentManifest.model_validate_json(row.manifest_json),
        created_at=row.created_at,
        updated_at=row.updated_at,
    )


def _api_key_from_row(row: AgentApiKeyRow) -> AgentApiKey:
    return AgentApiKey(
        id=row.id,
        agent_id=row.agent_id,
        label=row.label or "",
        key_hash=row.key_hash,
        created_at=row.created_at,
        revoked_at=row.revoked_at,
    )

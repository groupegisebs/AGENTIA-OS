"""Tests de la publication d'agent (idempotence, manifest cohérent)."""

import pytest

from agent_creator.config import Settings
from agent_creator.models.blueprint import Blueprint, BlueprintComponent
from agent_creator.models.deployment import Deployment, DeploymentStatus
from agent_creator.models.organization import Organization
from agent_creator.models.requirement import Requirement, SolutionType
from agent_creator.models.subscription import SubscriptionPlan


def _fake_settings() -> Settings:
    return Settings(database_url="sqlite+aiosqlite:///:memory:", jwt_secret="t")


def _blueprint() -> Blueprint:
    return Blueprint(
        conversation_id="conv-pub-1",
        title="Agent Factures",
        solution_type=SolutionType.HYBRID,
        solution_type_rationale="hybride",
        requirements=Requirement(
            objectives=["Traiter les factures"],
            domain="comptabilité",
            summary="Automatisation factures",
            completeness_score=0.9,
        ),
        components=[
            BlueprintComponent(name="Moteur IA", type="ai", description="Extraction"),
        ],
    )


def _deployment(blueprint: Blueprint) -> Deployment:
    return Deployment(
        organization_id="org-123",
        conversation_id="conv-pub-1",
        blueprint_id=blueprint.id,
        status=DeploymentStatus.DEPLOYED,
        deployment_cost=29.0,
    )


def _organization() -> Organization:
    return Organization(id="org-123", name="Cabinet Test", plan=SubscriptionPlan.PROFESSIONAL)


@pytest.mark.asyncio
async def test_publish_creates_agent(client) -> None:
    from agent_creator.db.repository import DbStore
    from agent_creator.db.session import async_session_factory
    from agent_creator.services.agent_publish import publish_from_deployment

    async with async_session_factory()() as session:
        db = DbStore(session)
        bp = _blueprint()
        await db.save_blueprint(bp)
        dep = _deployment(bp)
        org = _organization()
        await db.save_organization(org)

        agent = await publish_from_deployment(db, dep, bp, org, _fake_settings())
        assert agent.id
        assert agent.title == "Agent Factures"
        assert agent.manifest.system_prompt
        assert "comptabilité" in agent.manifest.system_prompt.lower() or "Traiter les factures" in agent.manifest.system_prompt
        await session.commit()


@pytest.mark.asyncio
async def test_publish_is_idempotent(client) -> None:
    from agent_creator.db.repository import DbStore
    from agent_creator.db.session import async_session_factory
    from agent_creator.services.agent_publish import publish_from_deployment

    async with async_session_factory()() as session:
        db = DbStore(session)
        bp = _blueprint()
        await db.save_blueprint(bp)
        dep = _deployment(bp)
        dep.id = "dep-idem-001"
        org = _organization()
        org.id = "org-idem-001"
        dep.organization_id = org.id
        await db.save_organization(org)

        a1 = await publish_from_deployment(db, dep, bp, org, _fake_settings())
        a2 = await publish_from_deployment(db, dep, bp, org, _fake_settings())
        assert a1.id == a2.id
        await session.commit()

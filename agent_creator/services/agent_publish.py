"""Publie un agent depuis un déploiement réussi (idempotent)."""

from agent_creator.config import Settings
from agent_creator.db.repository import DbStore
from agent_creator.models.agent import PublishedAgent
from agent_creator.models.blueprint import Blueprint
from agent_creator.models.deployment import Deployment
from agent_creator.models.organization import Organization
from agent_creator.services.manifest_builder import build_manifest


async def publish_from_deployment(
    db: DbStore,
    deployment: Deployment,
    blueprint: Blueprint,
    organization: Organization,
    settings: Settings,
    llm: object | None = None,
) -> PublishedAgent:
    """Crée ou retourne l'agent publié lié à ce déploiement (idempotent)."""
    existing = await db.get_published_agent_by_deployment(deployment.id)
    if existing:
        return existing

    manifest = await build_manifest(blueprint, organization.plan, settings, llm)

    agent = PublishedAgent(
        organization_id=organization.id,
        deployment_id=deployment.id,
        blueprint_id=blueprint.id,
        title=blueprint.title,
        description=blueprint.requirements.summary or blueprint.title,
        category=manifest.category,
        manifest=manifest,
    )
    await db.save_published_agent(agent)
    return agent

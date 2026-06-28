"""Orchestrateur du Publishing Center — coordonne les 7 étapes."""
from __future__ import annotations

from datetime import datetime

from agent_creator.config import Settings
from agent_creator.db.repository import DbStore
from agent_creator.models.agent import PublishedAgent
from agent_creator.models.publishing import (
    GeneratedContent,
    MarketplacePublication,
    PublicationStatus,
    PublishingJob,
    PublishingJobStatus,
    PublishingStep,
    SaleSettings,
)
from agent_creator.services.llm import LLMService
from agent_creator.services.publishing.agent_analyzer import analyze_agent
from agent_creator.services.publishing.giseboutique_adapter import GISEBoutiqueAdapter
from agent_creator.services.publishing.media_generator import generate_media
from agent_creator.services.publishing.product_generator import generate_product_content
from agent_creator.services.publishing.quality_scorer import calculate_scores


class PublishingService:
    def __init__(self, db: DbStore, llm: LLMService, settings: Settings) -> None:
        self._db = db
        self._llm = llm
        self._settings = settings
        self._adapters = {
            "giseboutique": GISEBoutiqueAdapter(settings),
        }

    # ─── Étape 1 : Analyse ────────────────────────────────────────────────────

    async def start_or_resume_job(self, agent: PublishedAgent, org_id: str) -> PublishingJob:
        """Crée un nouveau job ou retourne le dernier job en cours pour cet agent."""
        existing = await self._db.get_publishing_job_by_agent(agent.id)
        if existing and existing.status in (PublishingJobStatus.DRAFT, PublishingJobStatus.READY):
            return existing

        analysis = analyze_agent(agent)
        job = PublishingJob(
            agent_id=agent.id,
            organization_id=org_id,
            current_step=PublishingStep.GENERATE,
            analysis=analysis,
        )
        await self._db.save_publishing_job(job)
        return job

    # ─── Étape 2 : Génération contenu ─────────────────────────────────────────

    async def generate_content(self, job: PublishingJob) -> PublishingJob:
        if not job.analysis:
            raise ValueError("L'analyse doit être complétée avant la génération de contenu.")

        content = await generate_product_content(job.analysis, self._llm)
        job.content = content
        job.current_step = PublishingStep.MEDIA
        job.updated_at = datetime.utcnow()
        await self._db.save_publishing_job(job)
        return job

    def update_content(self, job: PublishingJob, updates: dict) -> PublishingJob:
        """Mise à jour partielle du contenu généré (modifications manuelles)."""
        if not job.content:
            job.content = GeneratedContent()
        current = job.content.model_dump()
        current.update({k: v for k, v in updates.items() if v is not None})
        job.content = GeneratedContent(**current)
        job.updated_at = datetime.utcnow()
        return job

    # ─── Étape 3 : Génération médias ──────────────────────────────────────────

    async def generate_media_assets(self, job: PublishingJob) -> PublishingJob:
        if not job.analysis or not job.content:
            raise ValueError("Analyse et contenu requis avant la génération des médias.")

        media = await generate_media(job.analysis, job.content, self._settings)
        job.media = media
        job.current_step = PublishingStep.SCORE
        job.updated_at = datetime.utcnow()
        await self._db.save_publishing_job(job)
        return job

    # ─── Étape 4 : Scores qualité ─────────────────────────────────────────────

    async def score_job(self, job: PublishingJob) -> PublishingJob:
        if not job.analysis or not job.content:
            raise ValueError("Analyse et contenu requis pour le calcul des scores.")

        scores = calculate_scores(
            analysis=job.analysis,
            content=job.content,
            media=job.media,
            settings=job.settings,
        )
        job.scores = scores
        job.current_step = PublishingStep.CONFIGURE
        job.updated_at = datetime.utcnow()
        await self._db.save_publishing_job(job)
        return job

    # ─── Étape 5 : Configuration ──────────────────────────────────────────────

    async def configure(self, job: PublishingJob, settings: SaleSettings) -> PublishingJob:
        job.settings = settings
        job.current_step = PublishingStep.PREVIEW
        job.status = PublishingJobStatus.READY
        job.updated_at = datetime.utcnow()
        await self._db.save_publishing_job(job)
        return job

    # ─── Étape 6 : Prévisualisation ───────────────────────────────────────────

    def build_preview(self, job: PublishingJob) -> dict:
        content = job.content or GeneratedContent()
        media = job.media
        settings = job.settings

        return {
            "commercial_title": content.commercial_title,
            "short_description": content.short_description,
            "long_description": content.long_description,
            "pitch": content.pitch,
            "features": content.features,
            "use_cases": content.use_cases,
            "target_audience": content.target_audience,
            "benefits": content.benefits,
            "faq": content.faq,
            "prerequisites": content.prerequisites,
            "user_documentation": content.user_documentation,
            "installation_guide": content.installation_guide,
            "meta_title": content.meta_title,
            "meta_description": content.meta_description,
            "seo_keywords": content.seo_keywords,
            "categories": content.categories,
            "tags": content.tags,
            "pricing_model": settings.pricing_model.value,
            "price": settings.price,
            "currency": settings.currency,
            "trial_days": settings.trial_days,
            "license_type": settings.license_type.value,
            "version": settings.version,
            "languages": settings.languages,
            "compatibility": settings.compatibility,
            "banner_url": media.banner_url if media else None,
            "icon_url": media.icon_url if media else None,
            "screenshots": media.screenshots if media else [],
            "scores": job.scores.model_dump() if job.scores else None,
        }

    # ─── Étape 7 : Publication ────────────────────────────────────────────────

    async def publish(
        self, job: PublishingJob, marketplace_ids: list[str]
    ) -> list[MarketplacePublication]:
        if not job.content:
            raise ValueError("Le contenu doit être généré avant la publication.")

        job.status = PublishingJobStatus.SUBMITTING
        job.current_step = PublishingStep.PUBLISHED
        await self._db.save_publishing_job(job)

        publications: list[MarketplacePublication] = []

        for marketplace_id in marketplace_ids:
            adapter = self._adapters.get(marketplace_id)
            if not adapter:
                pub = MarketplacePublication(
                    job_id=job.id,
                    agent_id=job.agent_id,
                    organization_id=job.organization_id,
                    marketplace_id=marketplace_id,
                    status=PublicationStatus.PENDING_REVIEW,
                    error=f"Marketplace '{marketplace_id}' non supportée.",
                )
            else:
                result = await adapter.publish_product(
                    job=job,
                    content=job.content,
                    media=job.media,
                    settings=job.settings,
                )
                pub = MarketplacePublication(
                    job_id=job.id,
                    agent_id=job.agent_id,
                    organization_id=job.organization_id,
                    marketplace_id=marketplace_id,
                    external_product_id=result.external_product_id,
                    external_url=result.external_url,
                    version=job.settings.version,
                    status=PublicationStatus(result.status) if result.status in PublicationStatus.__members__.values() else PublicationStatus.PENDING_REVIEW,
                    error=result.error,
                )

            await self._db.save_marketplace_publication(pub)
            publications.append(pub)

        job.status = PublishingJobStatus.PUBLISHED
        await self._db.save_publishing_job(job)
        return publications

    # ─── Synchronisation ──────────────────────────────────────────────────────

    async def sync_publication(
        self,
        publication: MarketplacePublication,
        job: PublishingJob,
        bump_version: bool = False,
    ) -> MarketplacePublication:
        if bump_version and job.settings:
            parts = job.settings.version.split(".")
            try:
                parts[-1] = str(int(parts[-1]) + 1)
                job.settings.version = ".".join(parts)
            except (ValueError, IndexError):
                job.settings.version = job.settings.version + ".1"
            await self._db.save_publishing_job(job)

        adapter = self._adapters.get(publication.marketplace_id)
        if not adapter or not job.content:
            publication.error = "Impossible de synchroniser"
            return publication

        result = await adapter.update_product(
            publication=publication,
            job=job,
            content=job.content,
            media=job.media,
            settings=job.settings,
        )

        publication.external_url = result.external_url or publication.external_url
        publication.last_synced_at = datetime.utcnow()
        publication.error = result.error
        if result.success:
            publication.status = PublicationStatus.ACTIVE
        await self._db.save_marketplace_publication(publication)
        return publication

    # ─── Helpers ──────────────────────────────────────────────────────────────

    async def get_job(self, job_id: str, org_id: str) -> PublishingJob | None:
        job = await self._db.get_publishing_job(job_id)
        if job and job.organization_id == org_id:
            return job
        return None

    async def list_publications(self, agent_id: str, org_id: str) -> list[MarketplacePublication]:
        return await self._db.list_marketplace_publications(agent_id, org_id)

    async def get_publication(self, pub_id: str, org_id: str) -> MarketplacePublication | None:
        pub = await self._db.get_marketplace_publication(pub_id)
        if pub and pub.organization_id == org_id:
            return pub
        return None

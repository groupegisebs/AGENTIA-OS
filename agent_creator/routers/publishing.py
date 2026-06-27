"""Publishing Center — 10 endpoints pour publier un agent sur les marketplaces."""
from __future__ import annotations

from fastapi import APIRouter, Depends, HTTPException, status

from agent_creator.db.repository import DbStore
from agent_creator.dependencies import UserContext, get_current_user, get_db_store
from agent_creator.models.publishing import SaleSettings
from agent_creator.schemas_publishing import (
    ConfigureRequest,
    PreviewResponse,
    PublicationResponse,
    PublishingJobResponse,
    PublishRequest,
    SyncRequest,
    UpdateContentRequest,
)
from agent_creator.services.publishing.publishing_service import PublishingService

router = APIRouter(prefix="/agents/{agent_id}/publishing", tags=["publishing"])


def _get_publishing_service(db: DbStore = Depends(get_db_store)) -> PublishingService:
    from agent_creator.main import llm_service, settings
    return PublishingService(db=db, llm=llm_service, settings=settings)


async def _get_agent(agent_id: str, ctx: UserContext, db: DbStore):
    agent = await db.get_published_agent(agent_id)
    if not agent:
        raise HTTPException(status_code=404, detail="Agent introuvable.")
    if agent.organization_id != ctx.organization.id:
        raise HTTPException(status_code=403, detail="Accès refusé.")
    return agent


# ─── Étape 1+2 : Démarrer le wizard (analyse + génération automatique) ────────

@router.post("", response_model=PublishingJobResponse, status_code=status.HTTP_201_CREATED)
async def start_publishing(
    agent_id: str,
    ctx: UserContext = Depends(get_current_user),
    db: DbStore = Depends(get_db_store),
    svc: PublishingService = Depends(_get_publishing_service),
):
    """Démarre le wizard Publishing Center pour cet agent.
    Analyse l'agent automatiquement (Étape 1) et crée le job de publication.
    """
    agent = await _get_agent(agent_id, ctx, db)
    job = await svc.start_or_resume_job(agent, ctx.organization.id)
    return PublishingJobResponse.from_job(job)


# ─── Étape 2 : Générer le contenu commercial ──────────────────────────────────

@router.post("/generate", response_model=PublishingJobResponse)
async def generate_content(
    agent_id: str,
    ctx: UserContext = Depends(get_current_user),
    db: DbStore = Depends(get_db_store),
    svc: PublishingService = Depends(_get_publishing_service),
):
    """Génère le contenu commercial via IA (titre, description, SEO, FAQ…)."""
    job = await db.get_publishing_job_by_agent(agent_id)
    if not job or job.organization_id != ctx.organization.id:
        raise HTTPException(status_code=404, detail="Job de publication introuvable. Lancez d'abord POST /publishing.")
    job = await svc.generate_content(job)
    return PublishingJobResponse.from_job(job)


@router.patch("/content", response_model=PublishingJobResponse)
async def update_content(
    agent_id: str,
    body: UpdateContentRequest,
    ctx: UserContext = Depends(get_current_user),
    db: DbStore = Depends(get_db_store),
    svc: PublishingService = Depends(_get_publishing_service),
):
    """Met à jour manuellement le contenu généré (toutes les sections sont modifiables)."""
    job = await db.get_publishing_job_by_agent(agent_id)
    if not job or job.organization_id != ctx.organization.id:
        raise HTTPException(status_code=404, detail="Job introuvable.")
    job = svc.update_content(job, body.model_dump(exclude_none=True))
    await db.save_publishing_job(job)
    return PublishingJobResponse.from_job(job)


# ─── Étape 3 : Générer les médias ─────────────────────────────────────────────

@router.post("/generate-media", response_model=PublishingJobResponse)
async def generate_media(
    agent_id: str,
    ctx: UserContext = Depends(get_current_user),
    db: DbStore = Depends(get_db_store),
    svc: PublishingService = Depends(_get_publishing_service),
):
    """Génère les médias produit via DALL-E (bannière, icône, captures)."""
    job = await db.get_publishing_job_by_agent(agent_id)
    if not job or job.organization_id != ctx.organization.id:
        raise HTTPException(status_code=404, detail="Job introuvable.")
    if not job.content:
        raise HTTPException(status_code=400, detail="Générez d'abord le contenu (POST /generate).")
    job = await svc.generate_media_assets(job)
    return PublishingJobResponse.from_job(job)


# ─── Étape 4 : Scores qualité ─────────────────────────────────────────────────

@router.post("/score", response_model=PublishingJobResponse)
async def score_job(
    agent_id: str,
    ctx: UserContext = Depends(get_current_user),
    db: DbStore = Depends(get_db_store),
    svc: PublishingService = Depends(_get_publishing_service),
):
    """Calcule les scores qualité (SEO, documentation, commercial, sécurité)."""
    job = await db.get_publishing_job_by_agent(agent_id)
    if not job or job.organization_id != ctx.organization.id:
        raise HTTPException(status_code=404, detail="Job introuvable.")
    if not job.content:
        raise HTTPException(status_code=400, detail="Contenu requis pour le scoring.")
    job = await svc.score_job(job)
    return PublishingJobResponse.from_job(job)


# ─── Étape 5 : Configurer les paramètres de vente ────────────────────────────

@router.post("/configure", response_model=PublishingJobResponse)
async def configure(
    agent_id: str,
    body: ConfigureRequest,
    ctx: UserContext = Depends(get_current_user),
    db: DbStore = Depends(get_db_store),
    svc: PublishingService = Depends(_get_publishing_service),
):
    """Configure les paramètres de vente : prix, licence, catégorie, langues…"""
    job = await db.get_publishing_job_by_agent(agent_id)
    if not job or job.organization_id != ctx.organization.id:
        raise HTTPException(status_code=404, detail="Job introuvable.")
    sale_settings = SaleSettings(**body.model_dump())
    job = await svc.configure(job, sale_settings)
    return PublishingJobResponse.from_job(job)


# ─── Étape 6 : Prévisualisation ───────────────────────────────────────────────

@router.get("/preview", response_model=PreviewResponse)
async def preview(
    agent_id: str,
    ctx: UserContext = Depends(get_current_user),
    db: DbStore = Depends(get_db_store),
    svc: PublishingService = Depends(_get_publishing_service),
):
    """Retourne la fiche produit exactement telle qu'elle apparaîtra sur GISEBoutique."""
    job = await db.get_publishing_job_by_agent(agent_id)
    if not job or job.organization_id != ctx.organization.id:
        raise HTTPException(status_code=404, detail="Job introuvable.")
    preview_data = svc.build_preview(job)
    return PreviewResponse(**preview_data)


# ─── Étape 7 : Publication ────────────────────────────────────────────────────

@router.post("/publish", response_model=list[PublicationResponse])
async def publish(
    agent_id: str,
    body: PublishRequest,
    ctx: UserContext = Depends(get_current_user),
    db: DbStore = Depends(get_db_store),
    svc: PublishingService = Depends(_get_publishing_service),
):
    """Publie l'agent sur les marketplaces cibles (GISEBoutique + futures)."""
    job = await db.get_publishing_job_by_agent(agent_id)
    if not job or job.organization_id != ctx.organization.id:
        raise HTTPException(status_code=404, detail="Job introuvable.")
    if not job.content:
        raise HTTPException(status_code=400, detail="Complétez les étapes generate et configure avant de publier.")
    publications = await svc.publish(job, body.marketplaces)
    return [PublicationResponse.from_pub(p) for p in publications]


# ─── Historique & synchronisation ────────────────────────────────────────────

@router.get("/publications", response_model=list[PublicationResponse])
async def list_publications(
    agent_id: str,
    ctx: UserContext = Depends(get_current_user),
    db: DbStore = Depends(get_db_store),
    svc: PublishingService = Depends(_get_publishing_service),
):
    """Liste toutes les publications de cet agent sur les marketplaces."""
    pubs = await svc.list_publications(agent_id, ctx.organization.id)
    return [PublicationResponse.from_pub(p) for p in pubs]


@router.patch("/publications/{pub_id}", response_model=PublicationResponse)
async def update_publication(
    agent_id: str,
    pub_id: str,
    body: UpdateContentRequest,
    ctx: UserContext = Depends(get_current_user),
    db: DbStore = Depends(get_db_store),
    svc: PublishingService = Depends(_get_publishing_service),
):
    """Met à jour le contenu d'une publication existante."""
    pub = await svc.get_publication(pub_id, ctx.organization.id)
    if not pub:
        raise HTTPException(status_code=404, detail="Publication introuvable.")
    job = await db.get_publishing_job_by_agent(agent_id)
    if not job:
        raise HTTPException(status_code=404, detail="Job introuvable.")
    job = svc.update_content(job, body.model_dump(exclude_none=True))
    await db.save_publishing_job(job)
    return PublicationResponse.from_pub(pub)


@router.post("/publications/{pub_id}/sync", response_model=PublicationResponse)
async def sync_publication(
    agent_id: str,
    pub_id: str,
    body: SyncRequest,
    ctx: UserContext = Depends(get_current_user),
    db: DbStore = Depends(get_db_store),
    svc: PublishingService = Depends(_get_publishing_service),
):
    """Synchronise la fiche produit après mise à jour de l'agent."""
    pub = await svc.get_publication(pub_id, ctx.organization.id)
    if not pub:
        raise HTTPException(status_code=404, detail="Publication introuvable.")
    job = await db.get_publishing_job_by_agent(agent_id)
    if not job:
        raise HTTPException(status_code=404, detail="Job introuvable.")
    pub = await svc.sync_publication(pub, job, bump_version=body.bump_version)
    return PublicationResponse.from_pub(pub)

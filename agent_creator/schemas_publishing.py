"""Schemas request/response pour le Publishing Center."""
from __future__ import annotations

from datetime import datetime

from pydantic import BaseModel, Field

from agent_creator.models.publishing import (
    AgentAnalysis,
    GeneratedContent,
    GeneratedMedia,
    LicenseType,
    MarketplacePublication,
    PricingModel,
    PublicationStatus,
    PublishingJob,
    PublishingJobStatus,
    PublishingStep,
    QualityScores,
    SaleSettings,
)


# ─── Réponses ────────────────────────────────────────────────────────────────

class PublishingJobResponse(BaseModel):
    id: str
    agent_id: str
    organization_id: str
    current_step: PublishingStep
    status: PublishingJobStatus
    analysis: AgentAnalysis | None
    content: GeneratedContent | None
    media: GeneratedMedia | None
    scores: QualityScores | None
    settings: SaleSettings
    target_marketplaces: list[str]
    created_at: datetime
    updated_at: datetime

    @classmethod
    def from_job(cls, job: PublishingJob) -> "PublishingJobResponse":
        return cls(**job.model_dump())


class PublicationResponse(BaseModel):
    id: str
    job_id: str
    agent_id: str
    organization_id: str
    marketplace_id: str
    external_product_id: str | None
    external_url: str | None
    version: str
    status: PublicationStatus
    published_at: datetime
    last_synced_at: datetime | None
    error: str | None

    @classmethod
    def from_pub(cls, pub: MarketplacePublication) -> "PublicationResponse":
        return cls(**pub.model_dump())


class PreviewResponse(BaseModel):
    """Prévisualisation de la fiche produit telle qu'elle apparaîtra sur GISEBoutique."""
    commercial_title: str
    short_description: str
    long_description: str
    pitch: str
    features: list[str]
    use_cases: list[str]
    target_audience: list[str]
    benefits: list[str]
    faq: list[dict[str, str]]
    prerequisites: list[str]
    user_documentation: str
    installation_guide: str
    meta_title: str
    meta_description: str
    seo_keywords: list[str]
    categories: list[str]
    tags: list[str]
    pricing_model: PricingModel
    price: float
    currency: str
    trial_days: int | None
    license_type: LicenseType
    version: str
    languages: list[str]
    compatibility: list[str]
    banner_url: str | None
    icon_url: str | None
    screenshots: list[str]
    scores: QualityScores | None


# ─── Requêtes ─────────────────────────────────────────────────────────────────

class ConfigureRequest(BaseModel):
    """Étape 5 — paramètres de vente définis par l'utilisateur."""
    pricing_model: PricingModel = PricingModel.FREE
    price: float = Field(default=0.0, ge=0)
    currency: str = "USD"
    trial_days: int | None = Field(default=None, ge=1, le=365)
    license_type: LicenseType = LicenseType.COMMERCIAL
    category: str = ""
    subcategory: str = ""
    languages: list[str] = Field(default_factory=list)
    compatibility: list[str] = Field(default_factory=list)
    version: str = "1.0.0"
    support_url: str | None = None
    support_email: str | None = None


class UpdateContentRequest(BaseModel):
    """Mise à jour partielle du contenu généré (tout champ est optionnel)."""
    commercial_title: str | None = None
    short_description: str | None = None
    long_description: str | None = None
    pitch: str | None = None
    use_cases: list[str] | None = None
    target_audience: list[str] | None = None
    benefits: list[str] | None = None
    features: list[str] | None = None
    faq: list[dict[str, str]] | None = None
    user_documentation: str | None = None
    installation_guide: str | None = None
    prerequisites: list[str] | None = None
    seo_keywords: list[str] | None = None
    meta_title: str | None = None
    meta_description: str | None = None
    categories: list[str] | None = None
    tags: list[str] | None = None


class PublishRequest(BaseModel):
    """Étape 7 — déclenche la publication sur les marketplaces cibles."""
    marketplaces: list[str] = Field(default_factory=lambda: ["giseboutique"])


class SyncRequest(BaseModel):
    """Demande de synchronisation après mise à jour de l'agent."""
    bump_version: bool = False
    changelog: str = ""

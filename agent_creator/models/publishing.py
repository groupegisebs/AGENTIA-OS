from __future__ import annotations

from datetime import datetime
from enum import Enum
from uuid import uuid4

from pydantic import BaseModel, Field


class PublishingStep(str, Enum):
    ANALYZE = "analyze"
    GENERATE = "generate"
    MEDIA = "media"
    SCORE = "score"
    CONFIGURE = "configure"
    PREVIEW = "preview"
    PUBLISHED = "published"


class PublishingJobStatus(str, Enum):
    DRAFT = "draft"
    READY = "ready"
    SUBMITTING = "submitting"
    PUBLISHED = "published"
    FAILED = "failed"


class PublicationStatus(str, Enum):
    ACTIVE = "active"
    PAUSED = "paused"
    ARCHIVED = "archived"
    PENDING_REVIEW = "pending_review"


class PricingModel(str, Enum):
    FREE = "free"
    ONE_TIME = "one_time"
    SUBSCRIPTION = "subscription"
    TRIAL_THEN_PAID = "trial_then_paid"


class LicenseType(str, Enum):
    COMMERCIAL = "commercial"
    OPEN_SOURCE = "open_source"
    ENTERPRISE = "enterprise"
    PRIVATE = "private"


# ─── Sous-modèles du contenu produit ─────────────────────────────────────────

class AgentAnalysis(BaseModel):
    """Extraction automatique des informations de l'agent (Étape 1)."""
    agent_id: str
    name: str
    description: str
    objective: str
    llm_providers: list[str] = Field(default_factory=list)
    tools: list[str] = Field(default_factory=list)
    connectors: list[str] = Field(default_factory=list)
    apis: list[str] = Field(default_factory=list)
    workflows: list[str] = Field(default_factory=list)
    permissions: list[str] = Field(default_factory=list)
    memory_enabled: bool = False
    dependencies: list[str] = Field(default_factory=list)
    technologies: list[str] = Field(default_factory=list)
    categories: list[str] = Field(default_factory=list)
    domain: str | None = None
    complexity_level: str = "medium"


class GeneratedContent(BaseModel):
    """Contenu commercial généré par l'IA (Étape 2)."""
    commercial_title: str = ""
    short_description: str = ""
    long_description: str = ""
    pitch: str = ""
    use_cases: list[str] = Field(default_factory=list)
    target_audience: list[str] = Field(default_factory=list)
    benefits: list[str] = Field(default_factory=list)
    features: list[str] = Field(default_factory=list)
    faq: list[dict[str, str]] = Field(default_factory=list)
    user_documentation: str = ""
    installation_guide: str = ""
    prerequisites: list[str] = Field(default_factory=list)
    seo_keywords: list[str] = Field(default_factory=list)
    meta_title: str = ""
    meta_description: str = ""
    schema_org_tags: dict = Field(default_factory=dict)
    categories: list[str] = Field(default_factory=list)
    tags: list[str] = Field(default_factory=list)
    similar_products: list[str] = Field(default_factory=list)


class GeneratedMedia(BaseModel):
    """Médias générés (Étape 3)."""
    banner_url: str | None = None
    icon_url: str | None = None
    logo_url: str | None = None
    screenshots: list[str] = Field(default_factory=list)
    marketing_images: list[str] = Field(default_factory=list)
    generation_status: str = "pending"
    error: str | None = None


class QualityScores(BaseModel):
    """Scores de qualité calculés (Étape 4)."""
    overall: float = 0.0
    seo: float = 0.0
    documentation: float = 0.0
    commercial: float = 0.0
    security: float = 0.0
    completeness: float = 0.0
    improvements: list[str] = Field(default_factory=list)


class SaleSettings(BaseModel):
    """Paramètres de vente configurés par l'utilisateur (Étape 5)."""
    pricing_model: PricingModel = PricingModel.FREE
    price: float = 0.0
    currency: str = "USD"
    trial_days: int | None = None
    license_type: LicenseType = LicenseType.COMMERCIAL
    category: str = ""
    subcategory: str = ""
    languages: list[str] = Field(default_factory=list)
    compatibility: list[str] = Field(default_factory=list)
    version: str = "1.0.0"
    support_url: str | None = None
    support_email: str | None = None


# ─── Entités principales ──────────────────────────────────────────────────────

class PublishingJob(BaseModel):
    """Wizard de publication en cours pour un agent."""
    id: str = Field(default_factory=lambda: str(uuid4()))
    agent_id: str
    organization_id: str
    current_step: PublishingStep = PublishingStep.ANALYZE
    status: PublishingJobStatus = PublishingJobStatus.DRAFT
    analysis: AgentAnalysis | None = None
    content: GeneratedContent | None = None
    media: GeneratedMedia | None = None
    scores: QualityScores | None = None
    settings: SaleSettings = Field(default_factory=SaleSettings)
    target_marketplaces: list[str] = Field(default_factory=lambda: ["giseboutique"])
    created_at: datetime = Field(default_factory=datetime.utcnow)
    updated_at: datetime = Field(default_factory=datetime.utcnow)


class MarketplacePublication(BaseModel):
    """Résultat d'une publication réussie sur une marketplace."""
    id: str = Field(default_factory=lambda: str(uuid4()))
    job_id: str
    agent_id: str
    organization_id: str
    marketplace_id: str
    external_product_id: str | None = None
    external_url: str | None = None
    version: str = "1.0.0"
    status: PublicationStatus = PublicationStatus.PENDING_REVIEW
    published_at: datetime = Field(default_factory=datetime.utcnow)
    last_synced_at: datetime | None = None
    error: str | None = None

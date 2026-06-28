"""Adaptateur GISEBoutique — publie un agent sur Agentia Market via l'API partenaire."""
from __future__ import annotations

import httpx

from agent_creator.config import Settings
from agent_creator.models.publishing import (
    GeneratedContent,
    GeneratedMedia,
    MarketplacePublication,
    PublishingJob,
    SaleSettings,
)
from agent_creator.services.publishing.marketplace_adapter import PublicationResult


class GISEBoutiqueAdapter:
    """Implémentation de MarketplaceAdapter pour Agentia Market (BoutiqueGisie)."""

    marketplace_id = "giseboutique"
    marketplace_name = "Agentia Market"

    def __init__(self, settings: Settings) -> None:
        self._base_url = settings.giseboutique_partner_url.rstrip("/")
        self._api_key = settings.giseboutique_partner_api_key

    @property
    def is_configured(self) -> bool:
        return bool(self._base_url and self._api_key)

    def _headers(self) -> dict[str, str]:
        return {
            "X-Partner-Key": self._api_key,
            "Content-Type": "application/json",
        }

    def _build_payload(
        self,
        job: PublishingJob,
        content: GeneratedContent,
        media: GeneratedMedia | None,
        settings: SaleSettings,
    ) -> dict:
        analysis = job.analysis
        return {
            "sourceAgentId": job.agent_id,
            "sourceOrganizationId": job.organization_id,
            "partnerSource": "agentiaos",
            "title": content.commercial_title or (analysis.name if analysis else "Agent IA"),
            "shortDescription": content.short_description,
            "longDescription": content.long_description,
            "pitch": content.pitch,
            "features": content.features,
            "useCases": content.use_cases,
            "targetAudience": content.target_audience,
            "benefits": content.benefits,
            "faq": content.faq,
            "prerequisites": content.prerequisites,
            "userDocumentation": content.user_documentation,
            "installationGuide": content.installation_guide,
            "seoKeywords": content.seo_keywords,
            "metaTitle": content.meta_title,
            "metaDescription": content.meta_description,
            "categories": content.categories,
            "tags": content.tags,
            "pricingModel": settings.pricing_model.value,
            "price": settings.price,
            "currency": settings.currency,
            "trialDays": settings.trial_days,
            "licenseType": settings.license_type.value,
            "category": settings.category,
            "subcategory": settings.subcategory,
            "languages": settings.languages,
            "compatibility": settings.compatibility,
            "version": settings.version,
            "supportUrl": settings.support_url,
            "supportEmail": settings.support_email,
            "bannerUrl": media.banner_url if media else None,
            "iconUrl": media.icon_url if media else None,
            "screenshots": media.screenshots if media else [],
        }

    async def publish_product(
        self,
        job: PublishingJob,
        content: GeneratedContent,
        media: GeneratedMedia | None,
        settings: SaleSettings,
    ) -> PublicationResult:
        if not self.is_configured:
            return PublicationResult(
                success=False,
                error="GISEBoutique partner API non configurée (GISEBOUTIQUE_PARTNER_URL / GISEBOUTIQUE_PARTNER_API_KEY).",
                status="failed",
            )

        payload = self._build_payload(job, content, media, settings)

        try:
            async with httpx.AsyncClient(timeout=30.0) as client:
                response = await client.post(
                    f"{self._base_url}/api/partner/products",
                    headers=self._headers(),
                    json=payload,
                )

            if response.status_code in (200, 201):
                data = response.json()
                return PublicationResult(
                    success=True,
                    external_product_id=str(data.get("productId", "")),
                    external_url=data.get("productUrl"),
                    status=data.get("status", "pending_review"),
                )
            else:
                return PublicationResult(
                    success=False,
                    error=f"GISEBoutique API error {response.status_code}: {response.text[:500]}",
                    status="failed",
                )
        except httpx.TimeoutException:
            return PublicationResult(
                success=False,
                error="Timeout lors de la connexion à GISEBoutique.",
                status="failed",
            )
        except Exception as exc:
            return PublicationResult(
                success=False,
                error=str(exc),
                status="failed",
            )

    async def update_product(
        self,
        publication: MarketplacePublication,
        job: PublishingJob,
        content: GeneratedContent,
        media: GeneratedMedia | None,
        settings: SaleSettings,
    ) -> PublicationResult:
        if not self.is_configured or not publication.external_product_id:
            return PublicationResult(success=False, error="Non configuré ou ID produit manquant.", status="failed")

        payload = self._build_payload(job, content, media, settings)
        payload["version"] = settings.version

        try:
            async with httpx.AsyncClient(timeout=30.0) as client:
                response = await client.patch(
                    f"{self._base_url}/api/partner/products/{publication.external_product_id}",
                    headers=self._headers(),
                    json=payload,
                )

            if response.status_code in (200, 204):
                data = response.json() if response.content else {}
                return PublicationResult(
                    success=True,
                    external_product_id=publication.external_product_id,
                    external_url=data.get("productUrl", publication.external_url),
                    status=data.get("status", "active"),
                )
            return PublicationResult(
                success=False,
                error=f"GISEBoutique update error {response.status_code}: {response.text[:300]}",
                status="failed",
            )
        except Exception as exc:
            return PublicationResult(success=False, error=str(exc), status="failed")

    async def get_product_status(self, external_product_id: str) -> str:
        if not self.is_configured:
            return "unknown"
        try:
            async with httpx.AsyncClient(timeout=10.0) as client:
                response = await client.get(
                    f"{self._base_url}/api/partner/products/{external_product_id}",
                    headers=self._headers(),
                )
            if response.status_code == 200:
                return response.json().get("status", "unknown")
        except Exception:
            pass
        return "unknown"

    async def unpublish_product(self, external_product_id: str) -> bool:
        if not self.is_configured:
            return False
        try:
            async with httpx.AsyncClient(timeout=10.0) as client:
                response = await client.delete(
                    f"{self._base_url}/api/partner/products/{external_product_id}",
                    headers=self._headers(),
                )
            return response.status_code in (200, 204)
        except Exception:
            return False

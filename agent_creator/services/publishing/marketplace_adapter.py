"""Couche d'abstraction marketplace — Protocol indépendant de toute implémentation."""
from __future__ import annotations

from dataclasses import dataclass
from typing import Protocol, runtime_checkable

from agent_creator.models.publishing import (
    GeneratedContent,
    GeneratedMedia,
    MarketplacePublication,
    PublishingJob,
    SaleSettings,
)


@dataclass
class PublicationResult:
    success: bool
    external_product_id: str | None = None
    external_url: str | None = None
    error: str | None = None
    status: str = "pending_review"


@runtime_checkable
class MarketplaceAdapter(Protocol):
    """Interface commune pour toutes les marketplaces cibles.

    Pour ajouter une nouvelle marketplace (AppSumo, ProductHunt…) :
    1. Créer une classe implémentant ce Protocol
    2. L'enregistrer dans `publishing_service.py`
    """

    marketplace_id: str
    marketplace_name: str

    async def publish_product(
        self,
        job: PublishingJob,
        content: GeneratedContent,
        media: GeneratedMedia | None,
        settings: SaleSettings,
    ) -> PublicationResult:
        """Publie le produit sur la marketplace et retourne le résultat."""
        ...

    async def update_product(
        self,
        publication: MarketplacePublication,
        job: PublishingJob,
        content: GeneratedContent,
        media: GeneratedMedia | None,
        settings: SaleSettings,
    ) -> PublicationResult:
        """Met à jour un produit existant."""
        ...

    async def get_product_status(self, external_product_id: str) -> str:
        """Retourne le statut du produit sur la marketplace."""
        ...

    async def unpublish_product(self, external_product_id: str) -> bool:
        """Dépublie / archive le produit."""
        ...

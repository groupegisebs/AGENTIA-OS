from __future__ import annotations

import logging
from abc import ABC, abstractmethod
from dataclasses import dataclass
from uuid import uuid4

from agent_creator.config import Settings
from agent_creator.models.subscription import SubscriptionPlan

logger = logging.getLogger(__name__)


@dataclass
class ChargeResult:
    success: bool
    charge_id: str | None
    error_message: str | None = None
    pending: bool = False
    checkout_url: str | None = None
    payment_code: str | None = None


@dataclass
class SubscriptionCheckoutResult:
    success: bool
    payment_code: str | None = None
    checkout_url: str | None = None
    error_message: str | None = None
    client_secret: str | None = None
    publishable_key: str | None = None


class PaymentProvider(ABC):
    """Interface de facturation (mock ou GiseBsPayGateway)."""

    @abstractmethod
    async def create_charge(
        self,
        amount: float,
        currency: str,
        customer_id: str | None,
        description: str,
        *,
        metadata: dict | None = None,
        customer_email: str | None = None,
        organization_id: str | None = None,
    ) -> ChargeResult:
        ...

    async def create_subscription_checkout(
        self,
        *,
        organization_id: str,
        customer_code: str,
        email: str,
        plan: SubscriptionPlan,
        full_name: str | None = None,
    ) -> SubscriptionCheckoutResult:
        return SubscriptionCheckoutResult(
            success=False,
            error_message="Abonnements non pris en charge par ce fournisseur.",
        )

    async def confirm_payment(self, payment_code: str) -> ChargeResult:
        return ChargeResult(
            success=False,
            charge_id=payment_code,
            error_message="Confirmation de paiement non disponible.",
        )

    @property
    def provider_name(self) -> str:
        return "mock"


class MockPaymentProvider(PaymentProvider):
    """Fournisseur de paiement simulé — aucune clé API requise."""

    def __init__(self, fail_rate: float = 0.0) -> None:
        self._fail_rate = fail_rate
        self._charges: list[dict] = []

    async def create_charge(
        self,
        amount: float,
        currency: str,
        customer_id: str | None,
        description: str,
        *,
        metadata: dict | None = None,
        customer_email: str | None = None,
        organization_id: str | None = None,
    ) -> ChargeResult:
        import random

        if random.random() < self._fail_rate:
            return ChargeResult(success=False, charge_id=None, error_message="Paiement refusé (simulation)")

        charge_id = f"ch_mock_{uuid4().hex[:16]}"
        self._charges.append(
            {
                "id": charge_id,
                "amount": amount,
                "currency": currency,
                "customer_id": customer_id,
                "description": description,
                "metadata": metadata,
            }
        )
        return ChargeResult(success=True, charge_id=charge_id)

    async def create_subscription_checkout(
        self,
        *,
        organization_id: str,
        customer_code: str,
        email: str,
        plan: SubscriptionPlan,
        full_name: str | None = None,
    ) -> SubscriptionCheckoutResult:
        if plan == SubscriptionPlan.FREE:
            return SubscriptionCheckoutResult(
                success=True,
                payment_code=f"sub_mock_{uuid4().hex[:12]}",
                checkout_url=None,
            )
        return SubscriptionCheckoutResult(
            success=True,
            payment_code=f"sub_mock_{uuid4().hex[:12]}",
            checkout_url=f"https://mock.agentia.local/subscribe/{plan.value}",
        )

    @property
    def charges(self) -> list[dict]:
        return list(self._charges)


class GiseBsPayGatewayProvider(PaymentProvider):
    """Facturation via GiseBsPayGateway (checkout Stripe + abonnements)."""

    def __init__(self, settings: Settings) -> None:
        from agent_creator.services.gisebs_pay_gateway import GiseBsPayGatewayClient

        self._settings = settings
        self._client = GiseBsPayGatewayClient(settings)

    @property
    def provider_name(self) -> str:
        return "gisebs_pay_gateway"

    async def create_charge(
        self,
        amount: float,
        currency: str,
        customer_id: str | None,
        description: str,
        *,
        metadata: dict | None = None,
        customer_email: str | None = None,
        organization_id: str | None = None,
    ) -> ChargeResult:
        org_id = organization_id or customer_id or "unknown"
        email = customer_email or f"{org_id}@agentia.factory"
        customer_code = customer_id or f"AF-{org_id}"

        charge_metadata = {
            "type": "deployment_charge",
            "amount_expected": amount,
            "currency": currency,
            "description": description,
            **(metadata or {}),
        }

        try:
            session = await self._client.create_checkout_session(
                customer_code=customer_code,
                email=email,
                product_code=self._settings.gisebs_pay_deployment_product_code,
                plan_code=self._settings.gisebs_pay_deployment_plan_code,
                full_name=customer_code,
                external_user_id=org_id,
                metadata=charge_metadata,
                embedded=False,
            )
        except Exception as exc:
            logger.exception("Échec création session checkout GiseBsPayGateway")
            return ChargeResult(
                success=False,
                charge_id=None,
                error_message=f"Impossible de joindre GiseBsPayGateway : {exc}",
            )

        payment = await self._client.wait_for_payment(session.payment_code)
        if payment and payment.is_successful:
            return ChargeResult(
                success=True,
                charge_id=session.payment_code,
                payment_code=session.payment_code,
            )

        return ChargeResult(
            success=False,
            charge_id=session.payment_code,
            payment_code=session.payment_code,
            checkout_url=session.checkout_url or None,
            pending=True,
            error_message=(
                "Paiement en attente — finalisez le checkout GiseBsPayGateway "
                f"puis appelez la confirmation avec le code {session.payment_code}."
            ),
        )

    async def create_subscription_checkout(
        self,
        *,
        organization_id: str,
        customer_code: str,
        email: str,
        plan: SubscriptionPlan,
        full_name: str | None = None,
    ) -> SubscriptionCheckoutResult:
        if plan == SubscriptionPlan.FREE:
            return SubscriptionCheckoutResult(success=True, payment_code=None, checkout_url=None)

        plan_code = self._settings.gisebs_pay_plan_code_for(plan)
        if not plan_code:
            return SubscriptionCheckoutResult(
                success=False,
                error_message=f"Aucun plan GiseBsPayGateway configuré pour {plan.value}.",
            )

        try:
            session = await self._client.create_checkout_session(
                customer_code=customer_code,
                email=email,
                product_code=self._settings.gisebs_pay_subscription_product_code,
                plan_code=plan_code,
                full_name=full_name or customer_code,
                external_user_id=organization_id,
                metadata={"type": "subscription", "plan": plan.value},
                embedded=False,
            )
        except Exception as exc:
            logger.exception("Échec checkout abonnement GiseBsPayGateway")
            return SubscriptionCheckoutResult(
                success=False,
                error_message=f"Impossible de créer l'abonnement : {exc}",
            )

        return SubscriptionCheckoutResult(
            success=True,
            payment_code=session.payment_code,
            checkout_url=session.checkout_url or None,
            client_secret=session.client_secret,
            publishable_key=session.publishable_key,
        )

    async def confirm_payment(self, payment_code: str) -> ChargeResult:
        try:
            payment = await self._client.wait_for_payment(
                payment_code,
                max_attempts=self._settings.gisebs_pay_poll_attempts,
            )
        except Exception as exc:
            return ChargeResult(
                success=False,
                charge_id=payment_code,
                payment_code=payment_code,
                error_message=f"Erreur lors de la confirmation : {exc}",
            )

        if payment and payment.is_successful:
            return ChargeResult(
                success=True,
                charge_id=payment_code,
                payment_code=payment_code,
            )

        status = payment.status if payment else "inconnu"
        return ChargeResult(
            success=False,
            charge_id=payment_code,
            payment_code=payment_code,
            pending=True,
            error_message=f"Paiement non finalisé (statut : {status}).",
        )


def create_payment_provider(settings: Settings) -> PaymentProvider:
    """Sélectionne GiseBsPayGateway si configuré, sinon mock."""
    if settings.gisebs_pay_configured:
        logger.info("Fournisseur de paiement : GiseBsPayGateway (%s)", settings.gisebs_pay_gateway_url)
        return GiseBsPayGatewayProvider(settings)
    logger.info("Fournisseur de paiement : mock (GiseBsPayGateway non configuré)")
    return MockPaymentProvider()

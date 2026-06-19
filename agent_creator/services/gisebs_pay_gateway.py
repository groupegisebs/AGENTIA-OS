"""Client HTTP pour GiseBsPayGateway (authentification JWT + API checkout/paiements)."""

from __future__ import annotations

import asyncio
import json
import logging
from dataclasses import dataclass
from datetime import datetime, timezone
from typing import Any

import httpx

from agent_creator.config import Settings

logger = logging.getLogger(__name__)

_SUCCEEDED_STATUSES = frozenset(
    {"succeeded", "paid", "complete", "active", "1", "2"}
)


@dataclass
class CheckoutSession:
    payment_code: str
    checkout_url: str
    session_id: str
    status: str
    client_secret: str | None = None
    publishable_key: str | None = None


@dataclass
class PaymentDetails:
    payment_code: str
    status: str
    amount: float
    currency: str
    paid_at: datetime | None = None
    failure_reason: str | None = None

    @property
    def is_successful(self) -> bool:
        if self.paid_at is not None:
            return True
        return self.status.lower() in _SUCCEEDED_STATUSES


class GiseBsPayGatewayClient:
    """Appelle l'API REST de GiseBsPayGateway (même surface que BoutiqueGisie)."""

    def __init__(self, settings: Settings) -> None:
        self._settings = settings
        self._access_token: str | None = None
        self._token_expires_at: datetime | None = None
        self._token_lock = asyncio.Lock()

    @property
    def is_configured(self) -> bool:
        return self._settings.gisebs_pay_configured

    async def create_checkout_session(
        self,
        *,
        customer_code: str,
        email: str,
        product_code: str,
        plan_code: str,
        full_name: str | None = None,
        external_user_id: str | None = None,
        metadata: dict[str, Any] | None = None,
        trial_days: int | None = None,
        embedded: bool = False,
    ) -> CheckoutSession:
        payload = {
            "customerCode": customer_code,
            "email": email,
            "fullName": full_name,
            "externalUserId": external_user_id,
            "productCode": product_code,
            "planCode": plan_code,
            "successUrl": self._settings.gisebs_pay_success_url,
            "cancelUrl": self._settings.gisebs_pay_cancel_url,
            "metadataJson": json.dumps(metadata) if metadata else None,
            "trialDays": trial_days,
            "embedded": embedded,
        }
        data = await self._request_json("POST", "api/checkout/session", json=payload)
        return CheckoutSession(
            payment_code=data["paymentCode"],
            checkout_url=data.get("checkoutUrl") or "",
            session_id=data.get("sessionId") or "",
            status=data.get("status") or "Pending",
            client_secret=data.get("clientSecret"),
            publishable_key=data.get("publishableKey"),
        )

    async def get_payment(self, payment_code: str) -> PaymentDetails | None:
        try:
            data = await self._request_json("GET", f"api/payments/{payment_code}")
        except httpx.HTTPStatusError as exc:
            if exc.response.status_code == 404:
                return None
            raise
        paid_at_raw = data.get("paidAt")
        paid_at = None
        if paid_at_raw:
            paid_at = datetime.fromisoformat(paid_at_raw.replace("Z", "+00:00")).astimezone(timezone.utc)
        return PaymentDetails(
            payment_code=data["paymentCode"],
            status=data.get("status") or "",
            amount=float(data.get("amount") or 0),
            currency=data.get("currency") or "EUR",
            paid_at=paid_at,
            failure_reason=data.get("failureReason"),
        )

    async def wait_for_payment(
        self,
        payment_code: str,
        *,
        max_attempts: int | None = None,
        delay_seconds: float | None = None,
    ) -> PaymentDetails | None:
        attempts = max_attempts if max_attempts is not None else self._settings.gisebs_pay_poll_attempts
        delay = delay_seconds if delay_seconds is not None else self._settings.gisebs_pay_poll_delay_seconds
        if attempts <= 0:
            return await self.get_payment(payment_code)

        for attempt in range(1, attempts + 1):
            payment = await self.get_payment(payment_code)
            if payment and payment.is_successful:
                return payment
            if attempt < attempts:
                await asyncio.sleep(delay)
        return await self.get_payment(payment_code)

    async def _request_json(self, method: str, path: str, **kwargs: Any) -> dict[str, Any]:
        token = await self._get_access_token()
        url = f"{self._settings.gisebs_pay_gateway_url.rstrip('/')}/{path.lstrip('/')}"
        headers = kwargs.pop("headers", {})
        headers["Authorization"] = f"Bearer {token}"
        timeout = self._settings.gisebs_pay_request_timeout_seconds
        async with httpx.AsyncClient(timeout=timeout) as client:
            response = await client.request(method, url, headers=headers, **kwargs)
            response.raise_for_status()
            return response.json()

    async def _get_access_token(self) -> str:
        now = datetime.now(timezone.utc)
        if self._access_token and self._token_expires_at and now < self._token_expires_at:
            return self._access_token

        async with self._token_lock:
            now = datetime.now(timezone.utc)
            if self._access_token and self._token_expires_at and now < self._token_expires_at:
                return self._access_token

            url = f"{self._settings.gisebs_pay_gateway_url.rstrip('/')}/api/auth/token"
            payload = {
                "appCode": self._settings.gisebs_pay_app_code,
                "apiKey": self._settings.gisebs_pay_api_key,
            }
            timeout = self._settings.gisebs_pay_request_timeout_seconds
            async with httpx.AsyncClient(timeout=timeout) as client:
                response = await client.post(url, json=payload)
                response.raise_for_status()
                data = response.json()

            expires_at_raw = data.get("expiresAt")
            expires_at = datetime.fromisoformat(expires_at_raw.replace("Z", "+00:00"))
            if expires_at.tzinfo is None:
                expires_at = expires_at.replace(tzinfo=timezone.utc)
            self._access_token = data["accessToken"]
            from datetime import timedelta

            self._token_expires_at = expires_at.astimezone(timezone.utc) - timedelta(seconds=60)
            return self._access_token

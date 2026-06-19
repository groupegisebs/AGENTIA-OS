import pytest

from agent_creator.config import Settings
from agent_creator.services.payment import MockPaymentProvider, create_payment_provider


@pytest.mark.asyncio
async def test_mock_payment_charge() -> None:
    provider = MockPaymentProvider()
    result = await provider.create_charge(29.0, "EUR", "AF-org-1", "Test déploiement")
    assert result.success is True
    assert result.charge_id is not None


def test_payment_provider_factory_mock() -> None:
    settings = Settings(
        gisebs_pay_gateway_url="",
        gisebs_pay_api_key="",
    )
    provider = create_payment_provider(settings)
    assert provider.provider_name == "mock"


def test_payment_provider_factory_gateway() -> None:
    settings = Settings(
        gisebs_pay_gateway_url="https://pay.example.com",
        gisebs_pay_app_code="AGENTIAOS",
        gisebs_pay_api_key="secret-key",
    )
    provider = create_payment_provider(settings)
    assert provider.provider_name == "gisebs_pay_gateway"

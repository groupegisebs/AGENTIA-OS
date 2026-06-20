import pytest

from agent_creator.config import Settings
from agent_creator.services.billing import BillingService
from agent_creator.services.payment import MockPaymentProvider


@pytest.mark.asyncio
async def test_register_login_and_me(client) -> None:
    reg = await client.post(
        "/auth/register",
        json={
            "email": "test@example.com",
            "password": "password123",
            "full_name": "Jean Test",
            "organization_name": "Cabinet Test",
        },
    )
    assert reg.status_code == 201
    token = reg.json()["access_token"]

    me = await client.get("/auth/me", headers={"Authorization": f"Bearer {token}"})
    assert me.status_code == 200
    data = me.json()
    assert data["user"]["email"] == "test@example.com"
    assert data["organization"]["name"] == "Cabinet Test"
    assert data["plan_name"] == "Gratuit"

    login = await client.post(
        "/auth/login",
        json={"email": "test@example.com", "password": "password123"},
    )
    assert login.status_code == 200
    assert login.json()["access_token"]


@pytest.mark.asyncio
async def test_conversation_requires_auth(client) -> None:
    res = await client.post("/conversations", json={"message": "Bonjour"})
    assert res.status_code == 401


@pytest.mark.asyncio
async def test_architect_requires_auth(client) -> None:
    res = await client.post("/architect/analyze", json={"description": "Automatiser les emails"})
    assert res.status_code == 401


@pytest.mark.asyncio
async def test_plans_requires_auth(client) -> None:
    res = await client.get("/plans")
    assert res.status_code == 401


@pytest.mark.asyncio
async def test_tenant_isolation(client) -> None:
    async def register(email: str) -> str:
        r = await client.post(
            "/auth/register",
            json={
                "email": email,
                "password": "password123",
                "full_name": "User",
                "organization_name": "Org",
            },
        )
        return r.json()["access_token"]

    token_a = await register("a@example.com")
    token_b = await register("b@example.com")

    conv = await client.post(
        "/conversations",
        json={"message": "Secret org A"},
        headers={"Authorization": f"Bearer {token_a}"},
    )
    conv_id = conv.json()["conversation"]["id"]

    forbidden = await client.get(
        f"/conversations/{conv_id}",
        headers={"Authorization": f"Bearer {token_b}"},
    )
    assert forbidden.status_code == 404


def test_deploy_plan_tier_mapping() -> None:
    billing = BillingService(Settings(), MockPaymentProvider())
    assert billing.resolve_deploy_plan_code(1.0) == "DEPLOY-S"
    assert billing.resolve_deploy_plan_code(1.3) == "DEPLOY-M"
    assert billing.resolve_deploy_plan_code(1.8) == "DEPLOY-L"

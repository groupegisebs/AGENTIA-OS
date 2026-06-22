"""Tests API agents : listing, invoke, auth, isolation org, marketplace."""

import pytest


async def _register(client, email: str, org: str) -> str:
    r = await client.post(
        "/auth/register",
        json={"email": email, "password": "pass1234", "full_name": "Test", "organization_name": org},
    )
    assert r.status_code == 201
    return r.json()["access_token"]


async def _create_agent(client, token: str) -> str:
    """Conversation → blueprint → deploy → returns published agent id."""
    headers = {"Authorization": f"Bearer {token}"}

    conv = await client.post("/conversations", json={"message": "Je veux automatiser mes factures"}, headers=headers)
    assert conv.status_code == 201
    conv_id = conv.json()["conversation"]["id"]

    msg = await client.post(f"/conversations/{conv_id}/messages", json={"message": "Volume 50/jour, Sage"}, headers=headers)
    assert msg.status_code == 200

    bp = await client.get(f"/conversations/{conv_id}/blueprint", headers=headers)
    assert bp.status_code == 200

    deploy = await client.post(f"/conversations/{conv_id}/deploy", headers=headers)
    assert deploy.status_code == 201

    agents = await client.get("/agents", headers=headers)
    assert agents.status_code == 200
    items = agents.json()
    assert len(items) >= 1
    return items[0]["id"]


@pytest.mark.asyncio
async def test_agents_require_auth(client) -> None:
    r = await client.get("/agents")
    assert r.status_code == 401


@pytest.mark.asyncio
async def test_marketplace_requires_auth(client) -> None:
    r = await client.get("/marketplace/agents")
    assert r.status_code == 401


@pytest.mark.asyncio
async def test_list_agents_empty_initially(client) -> None:
    token = await _register(client, "empty@test.com", "Empty Org")
    r = await client.get("/agents", headers={"Authorization": f"Bearer {token}"})
    assert r.status_code == 200
    assert r.json() == []


@pytest.mark.asyncio
async def test_agent_published_after_deploy(client) -> None:
    token = await _register(client, "creator@test.com", "Creator Org")
    agent_id = await _create_agent(client, token)
    assert agent_id

    detail = await client.get(f"/agents/{agent_id}", headers={"Authorization": f"Bearer {token}"})
    assert detail.status_code == 200
    data = detail.json()
    assert data["title"]
    assert data["manifest"]["system_prompt"]
    assert data["visibility"] == "private"
    assert data["status"] == "active"


@pytest.mark.asyncio
async def test_tenant_isolation(client) -> None:
    token_a = await _register(client, "orgA@test.com", "Org A")
    token_b = await _register(client, "orgB@test.com", "Org B")

    agent_id = await _create_agent(client, token_a)

    r = await client.get(f"/agents/{agent_id}", headers={"Authorization": f"Bearer {token_b}"})
    assert r.status_code == 403


@pytest.mark.asyncio
async def test_invoke_agent(client) -> None:
    token = await _register(client, "invoker@test.com", "Invoker Org")
    agent_id = await _create_agent(client, token)

    r = await client.post(
        f"/agents/{agent_id}/invoke",
        json={"message": "Quel est ton rôle ?"},
        headers={"Authorization": f"Bearer {token}"},
    )
    assert r.status_code == 200
    data = r.json()
    assert "reply" in data
    assert "invocation_id" in data


@pytest.mark.asyncio
async def test_invoke_message_too_long(client) -> None:
    token = await _register(client, "long@test.com", "Long Org")
    agent_id = await _create_agent(client, token)

    long_msg = "x" * 5000
    r = await client.post(
        f"/agents/{agent_id}/invoke",
        json={"message": long_msg},
        headers={"Authorization": f"Bearer {token}"},
    )
    assert r.status_code == 422


@pytest.mark.asyncio
async def test_update_visibility(client) -> None:
    token = await _register(client, "vis@test.com", "Vis Org")
    agent_id = await _create_agent(client, token)

    r = await client.patch(
        f"/agents/{agent_id}",
        json={"visibility": "public"},
        headers={"Authorization": f"Bearer {token}"},
    )
    assert r.status_code == 200
    assert r.json()["visibility"] == "public"


@pytest.mark.asyncio
async def test_public_agent_in_marketplace(client) -> None:
    token_creator = await _register(client, "pub_creator@test.com", "Pub Creator Org")
    token_other = await _register(client, "pub_consumer@test.com", "Consumer Org")
    agent_id = await _create_agent(client, token_creator)

    await client.patch(
        f"/agents/{agent_id}",
        json={"visibility": "public"},
        headers={"Authorization": f"Bearer {token_creator}"},
    )

    market = await client.get("/marketplace/agents", headers={"Authorization": f"Bearer {token_other}"})
    assert market.status_code == 200
    ids = [a["id"] for a in market.json()]
    assert agent_id in ids


@pytest.mark.asyncio
async def test_private_agent_not_in_marketplace(client) -> None:
    token = await _register(client, "priv@test.com", "Priv Org")
    token_other = await _register(client, "priv_other@test.com", "Other Org")
    agent_id = await _create_agent(client, token)

    market = await client.get("/marketplace/agents", headers={"Authorization": f"Bearer {token_other}"})
    assert market.status_code == 200
    ids = [a["id"] for a in market.json()]
    assert agent_id not in ids


@pytest.mark.asyncio
async def test_api_key_create_and_invoke(client) -> None:
    token = await _register(client, "apikey@test.com", "ApiKey Org")
    agent_id = await _create_agent(client, token)

    key_resp = await client.post(
        f"/agents/{agent_id}/api-keys",
        json={"label": "test key"},
        headers={"Authorization": f"Bearer {token}"},
    )
    assert key_resp.status_code == 201
    raw_key = key_resp.json()["key"]
    assert raw_key.startswith("agt_")

    r = await client.post(
        f"/agents/{agent_id}/invoke",
        json={"message": "Bonjour depuis la clé API"},
        headers={"X-Agent-Key": raw_key},
    )
    assert r.status_code == 200


@pytest.mark.asyncio
async def test_other_org_cannot_patch_agent(client) -> None:
    token_a = await _register(client, "patch_a@test.com", "Patch Org A")
    token_b = await _register(client, "patch_b@test.com", "Patch Org B")
    agent_id = await _create_agent(client, token_a)

    r = await client.patch(
        f"/agents/{agent_id}",
        json={"visibility": "public"},
        headers={"Authorization": f"Bearer {token_b}"},
    )
    assert r.status_code in (403, 404)

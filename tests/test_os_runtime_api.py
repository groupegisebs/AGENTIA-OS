"""Tests API des fondations OS runtime (V1 incrémentale)."""

import pytest


async def _register(client, email: str, org: str) -> str:
    response = await client.post(
        "/auth/register",
        json={"email": email, "password": "pass1234", "full_name": "Test", "organization_name": org},
    )
    assert response.status_code == 201
    return response.json()["access_token"]


async def _create_agent(client, token: str) -> str:
    headers = {"Authorization": f"Bearer {token}"}
    conv = await client.post(
        "/conversations", json={"message": "Je veux un agent de support CRM"}, headers=headers
    )
    assert conv.status_code == 201
    conversation_id = conv.json()["conversation"]["id"]

    reply = await client.post(
        f"/conversations/{conversation_id}/messages",
        json={"message": "Intégration Salesforce et email"},
        headers=headers,
    )
    assert reply.status_code == 200

    blueprint = await client.get(f"/conversations/{conversation_id}/blueprint", headers=headers)
    assert blueprint.status_code == 200

    deploy = await client.post(f"/conversations/{conversation_id}/deploy", headers=headers)
    assert deploy.status_code == 201

    agents = await client.get("/agents", headers=headers)
    assert agents.status_code == 200
    assert agents.json()
    return agents.json()[0]["id"]


@pytest.mark.asyncio
async def test_os_runtime_requires_auth(client) -> None:
    response = await client.get("/os/v1/agents/any/runtime")
    assert response.status_code == 401


@pytest.mark.asyncio
async def test_runtime_lifecycle_and_events(client) -> None:
    token = await _register(client, "osruntime@test.com", "OS Runtime Org")
    headers = {"Authorization": f"Bearer {token}"}
    agent_id = await _create_agent(client, token)

    current = await client.get(f"/os/v1/agents/{agent_id}/runtime", headers=headers)
    assert current.status_code == 200
    assert current.json()["lifecycle_state"] == "stopped"

    running = await client.put(
        f"/os/v1/agents/{agent_id}/runtime",
        json={"state": "running", "reason": "start run"},
        headers=headers,
    )
    assert running.status_code == 200
    assert running.json()["lifecycle_state"] == "running"
    assert running.json()["started_at"] is not None

    failed = await client.put(
        f"/os/v1/agents/{agent_id}/runtime",
        json={"state": "failed", "error": "LLM timeout"},
        headers=headers,
    )
    assert failed.status_code == 200
    assert failed.json()["lifecycle_state"] == "failed"
    assert failed.json()["last_error"] == "LLM timeout"

    events = await client.get(f"/os/v1/agents/{agent_id}/events", headers=headers)
    assert events.status_code == 200
    runtime_events = [e for e in events.json() if e["event_type"] == "runtime.state.changed"]
    assert len(runtime_events) >= 2


@pytest.mark.asyncio
async def test_capabilities_memory_and_custom_events(client) -> None:
    token = await _register(client, "oscaps@test.com", "OS Caps Org")
    headers = {"Authorization": f"Bearer {token}"}
    agent_id = await _create_agent(client, token)

    capabilities = await client.put(
        f"/os/v1/agents/{agent_id}/capabilities",
        json={
            "tools": ["web.search", "crm.lookup", "web.search"],
            "actions": ["triage", "answer"],
            "events": ["ticket.created"],
        },
        headers=headers,
    )
    assert capabilities.status_code == 200
    caps_data = capabilities.json()
    assert caps_data["tools"] == ["crm.lookup", "web.search"]
    assert caps_data["actions"] == ["answer", "triage"]
    assert caps_data["events"] == ["ticket.created"]

    memory_one = await client.post(
        f"/os/v1/agents/{agent_id}/memory",
        json={"namespace": "support", "text": "Client ACME préfère le français."},
        headers=headers,
    )
    assert memory_one.status_code == 201

    memory_two = await client.post(
        f"/os/v1/agents/{agent_id}/memory",
        json={"namespace": "sales", "text": "Prospect BETA cible Q4."},
        headers=headers,
    )
    assert memory_two.status_code == 201

    support_memory = await client.get(
        f"/os/v1/agents/{agent_id}/memory",
        params={"namespace": "support"},
        headers=headers,
    )
    assert support_memory.status_code == 200
    assert len(support_memory.json()) == 1
    assert support_memory.json()[0]["namespace"] == "support"

    custom_event = await client.post(
        f"/os/v1/agents/{agent_id}/events",
        json={
            "event_type": "ticket.created",
            "payload": {"ticket_id": "T-100"},
            "metadata": {"channel": "email"},
        },
        headers=headers,
    )
    assert custom_event.status_code == 201
    assert custom_event.json()["metadata"]["channel"] == "email"


@pytest.mark.asyncio
async def test_os_runtime_tenant_isolation(client) -> None:
    token_a = await _register(client, "osA@test.com", "OS A")
    token_b = await _register(client, "osB@test.com", "OS B")
    agent_id = await _create_agent(client, token_a)

    forbidden = await client.get(
        f"/os/v1/agents/{agent_id}/runtime",
        headers={"Authorization": f"Bearer {token_b}"},
    )
    assert forbidden.status_code == 403

import pytest

from agent_creator.models.os_runtime import AgentEvent
from agent_creator.services.os_foundation import AgentEventBus


class _FakeDb:
    def __init__(self) -> None:
        self.events: list[AgentEvent] = []

    async def save_agent_event(self, event: AgentEvent) -> AgentEvent:
        self.events.append(event)
        return event


@pytest.mark.asyncio
async def test_event_bus_publish_and_subscribe_with_metadata_filter() -> None:
    bus = AgentEventBus()
    db = _FakeDb()
    received: list[AgentEvent] = []

    async def on_event(event: AgentEvent) -> None:
        received.append(event)

    bus.subscribe(
        on_event,
        event_type="ticket.created",
        metadata_contains={"channel": "email"},
    )

    await bus.publish(
        db,
        AgentEvent(
            agent_id="agent-1",
            organization_id="org-1",
            event_type="ticket.created",
            payload={"id": "T-1"},
            metadata={"channel": "chat"},
        ),
    )
    await bus.publish(
        db,
        AgentEvent(
            agent_id="agent-1",
            organization_id="org-1",
            event_type="ticket.created",
            payload={"id": "T-2"},
            metadata={"channel": "email"},
        ),
    )

    assert len(db.events) == 2
    assert len(received) == 1
    assert received[0].payload["id"] == "T-2"

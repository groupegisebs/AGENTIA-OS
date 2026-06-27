from __future__ import annotations

from collections.abc import Awaitable, Callable
from dataclasses import dataclass
from datetime import datetime
from uuid import uuid4

from agent_creator.db.repository import DbStore
from agent_creator.models.os_runtime import (
    AgentCapabilityRegistry,
    AgentEvent,
    AgentLifecycleState,
    AgentMemoryEntry,
    AgentRuntime,
)

EventHandler = Callable[[AgentEvent], Awaitable[None] | None]


@dataclass
class EventSubscription:
    id: str
    event_type: str | None
    metadata_contains: dict
    handler: EventHandler


class AgentEventBus:
    """Event bus applicatif minimal avec persistance DB."""

    def __init__(self) -> None:
        self._subscriptions: dict[str, EventSubscription] = {}

    def subscribe(
        self,
        handler: EventHandler,
        *,
        event_type: str | None = None,
        metadata_contains: dict | None = None,
    ) -> str:
        sub_id = str(uuid4())
        self._subscriptions[sub_id] = EventSubscription(
            id=sub_id,
            event_type=event_type,
            metadata_contains=metadata_contains or {},
            handler=handler,
        )
        return sub_id

    def unsubscribe(self, subscription_id: str) -> None:
        self._subscriptions.pop(subscription_id, None)

    async def publish(self, db: DbStore, event: AgentEvent) -> AgentEvent:
        persisted = await db.save_agent_event(event)
        for subscription in self._subscriptions.values():
            if not self._matches(subscription, persisted):
                continue
            callback_result = subscription.handler(persisted)
            if callback_result is not None:
                await callback_result
        return persisted

    @staticmethod
    def _matches(subscription: EventSubscription, event: AgentEvent) -> bool:
        if subscription.event_type and subscription.event_type != event.event_type:
            return False
        for key, expected in subscription.metadata_contains.items():
            if event.metadata.get(key) != expected:
                return False
        return True


class AgentOSService:
    def __init__(self, event_bus: AgentEventBus) -> None:
        self._event_bus = event_bus

    async def get_runtime(self, db: DbStore, agent_id: str, organization_id: str) -> AgentRuntime:
        runtime = await db.get_agent_runtime(agent_id)
        if runtime:
            return runtime
        runtime = AgentRuntime(agent_id=agent_id, organization_id=organization_id)
        await db.save_agent_runtime(runtime)
        return runtime

    async def set_runtime_state(
        self,
        db: DbStore,
        *,
        agent_id: str,
        organization_id: str,
        lifecycle_state: AgentLifecycleState,
        reason: str | None = None,
        error: str | None = None,
    ) -> AgentRuntime:
        runtime = await self.get_runtime(db, agent_id, organization_id)
        now = datetime.utcnow()
        runtime.lifecycle_state = lifecycle_state
        runtime.updated_at = now
        if lifecycle_state == AgentLifecycleState.RUNNING:
            runtime.started_at = now
            runtime.suspended_at = None
            runtime.stopped_at = None
            runtime.last_error = None
        elif lifecycle_state == AgentLifecycleState.SUSPENDED:
            runtime.suspended_at = now
        elif lifecycle_state == AgentLifecycleState.STOPPED:
            runtime.stopped_at = now
            runtime.last_error = None
        elif lifecycle_state == AgentLifecycleState.FAILED:
            runtime.last_error = error or "Runtime failure"
        await db.save_agent_runtime(runtime)

        await self._event_bus.publish(
            db,
            AgentEvent(
                agent_id=agent_id,
                organization_id=organization_id,
                event_type="runtime.state.changed",
                payload={"state": lifecycle_state.value, "reason": reason, "error": runtime.last_error},
                metadata={"source": "os.runtime"},
            ),
        )
        return runtime

    async def get_capabilities(
        self, db: DbStore, agent_id: str, organization_id: str
    ) -> AgentCapabilityRegistry:
        registry = await db.get_agent_capability_registry(agent_id)
        if registry:
            return registry
        registry = AgentCapabilityRegistry(agent_id=agent_id, organization_id=organization_id)
        await db.save_agent_capability_registry(registry)
        return registry

    async def set_capabilities(
        self,
        db: DbStore,
        *,
        agent_id: str,
        organization_id: str,
        tools: list[str],
        actions: list[str],
        events: list[str],
    ) -> AgentCapabilityRegistry:
        registry = await self.get_capabilities(db, agent_id, organization_id)
        registry.tools = sorted(set(tools))
        registry.actions = sorted(set(actions))
        registry.events = sorted(set(events))
        registry.updated_at = datetime.utcnow()
        await db.save_agent_capability_registry(registry)
        return registry

    async def publish_event(
        self,
        db: DbStore,
        *,
        agent_id: str,
        organization_id: str,
        event_type: str,
        payload: dict | None = None,
        metadata: dict | None = None,
    ) -> AgentEvent:
        event = AgentEvent(
            agent_id=agent_id,
            organization_id=organization_id,
            event_type=event_type,
            payload=payload or {},
            metadata=metadata or {},
        )
        return await self._event_bus.publish(db, event)

    async def list_events(self, db: DbStore, agent_id: str, limit: int = 50) -> list[AgentEvent]:
        return await db.list_agent_events(agent_id, limit=limit)

    async def append_memory(
        self,
        db: DbStore,
        *,
        agent_id: str,
        organization_id: str,
        namespace: str,
        text: str,
        metadata: dict | None = None,
    ) -> AgentMemoryEntry:
        entry = AgentMemoryEntry(
            agent_id=agent_id,
            organization_id=organization_id,
            namespace=namespace,
            text=text,
            metadata=metadata or {},
        )
        await db.save_agent_memory_entry(entry)
        await self._event_bus.publish(
            db,
            AgentEvent(
                agent_id=agent_id,
                organization_id=organization_id,
                event_type="memory.entry.added",
                payload={"entry_id": entry.id, "namespace": namespace},
                metadata={"source": "os.memory", "namespace": namespace},
            ),
        )
        return entry

    async def list_memory(
        self,
        db: DbStore,
        *,
        agent_id: str,
        namespace: str | None = None,
        limit: int = 50,
    ) -> list[AgentMemoryEntry]:
        return await db.list_agent_memory_entries(agent_id, namespace=namespace, limit=limit)

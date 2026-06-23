"""Exécution in-process d'un agent publié via LLMService."""

import time
from datetime import datetime

from fastapi import HTTPException

from agent_creator.db.repository import DbStore
from agent_creator.models.agent import AgentInvocation, InvocationResponse, PublishedAgent
from agent_creator.services.llm import LLMService


class InvocationError(Exception):
    pass


async def invoke_agent(
    agent: PublishedAgent,
    message: str,
    organization_id: str,
    db: DbStore,
    llm: LLMService,
) -> InvocationResponse:
    manifest = agent.manifest
    policies = manifest.policies

    if len(message) > policies.max_input_chars:
        raise HTTPException(
            status_code=422,
            detail=f"Message trop long ({len(message)} car.) — limite : {policies.max_input_chars}.",
        )

    messages = [
        {"role": "system", "content": manifest.system_prompt},
        {"role": "user", "content": message},
    ]

    t0 = time.monotonic()
    status = "success"
    reply = ""
    try:
        reply = await llm.chat(messages)
    except Exception as exc:
        status = "error"
        raise HTTPException(status_code=502, detail=f"Erreur LLM : {exc}") from exc
    finally:
        latency_ms = int((time.monotonic() - t0) * 1000)
        inv = AgentInvocation(
            agent_id=agent.id,
            organization_id=organization_id,
            input_chars=len(message),
            latency_ms=latency_ms,
            status=status,
            created_at=datetime.utcnow(),
        )
        await db.save_agent_invocation(inv)

    return InvocationResponse(
        invocation_id=inv.id,
        reply=reply,
        agent_id=agent.id,
        latency_ms=latency_ms,
    )

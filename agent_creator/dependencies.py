from __future__ import annotations

from collections.abc import AsyncIterator
from dataclasses import dataclass
from typing import TYPE_CHECKING

from fastapi import Depends, HTTPException, Request, status
from fastapi.security import HTTPAuthorizationCredentials, HTTPBearer
from sqlalchemy.ext.asyncio import AsyncSession

from agent_creator.config import get_settings
from agent_creator.db.repository import DbStore
from agent_creator.db.session import async_session_factory
from agent_creator.models.organization import Organization
from agent_creator.models.user import Membership, User
from agent_creator.services.auth import AuthError, AuthService

if TYPE_CHECKING:
    from agent_creator.services.oauth import OAuthService

_bearer = HTTPBearer(auto_error=False)


async def get_db() -> AsyncIterator[AsyncSession]:
    factory = async_session_factory()
    session = factory()
    try:
        yield session
        await session.commit()
    except Exception:
        await session.rollback()
        raise
    finally:
        await session.close()


def get_auth_service() -> AuthService:
    return AuthService(get_settings())


def get_oauth_service() -> OAuthService:
    from agent_creator.services.oauth import OAuthService

    return OAuthService(get_settings())


async def get_db_store(session: AsyncSession = Depends(get_db)) -> DbStore:
    return DbStore(session)


@dataclass
class UserContext:
    user: User
    organization: Organization
    membership: Membership


async def get_current_user(
    credentials: HTTPAuthorizationCredentials | None = Depends(_bearer),
    session: AsyncSession = Depends(get_db),
    auth: AuthService = Depends(get_auth_service),
) -> UserContext:
    if not credentials or credentials.scheme.lower() != "bearer":
        raise HTTPException(status_code=status.HTTP_401_UNAUTHORIZED, detail="Authentification requise")
    try:
        user_id, org_id = auth.decode_token(credentials.credentials)
        user, org, membership = await auth.get_user_context(session, user_id, org_id)
    except AuthError as exc:
        raise HTTPException(status_code=status.HTTP_401_UNAUTHORIZED, detail=str(exc)) from exc
    return UserContext(user=user, organization=org, membership=membership)


async def get_optional_user(
    credentials: HTTPAuthorizationCredentials | None = Depends(_bearer),
    session: AsyncSession = Depends(get_db),
    auth: AuthService = Depends(get_auth_service),
) -> UserContext | None:
    if not credentials or credentials.scheme.lower() != "bearer":
        return None
    try:
        user_id, org_id = auth.decode_token(credentials.credentials)
        user, org, membership = await auth.get_user_context(session, user_id, org_id)
        return UserContext(user=user, organization=org, membership=membership)
    except AuthError:
        return None


async def get_deployment_service(db: DbStore = Depends(get_db_store)):
    from agent_creator.main import billing_service, llm_service
    from agent_creator.services.deployment import DeploymentService

    return DeploymentService(db, billing_service, llm_service)


def get_agent_os_service(request: Request):
    service = getattr(request.app.state, "agent_os_service", None)
    if service is not None:
        return service
    from agent_creator.main import agent_os_service

    return agent_os_service


async def get_agent_consumer(
    request: Request,
    credentials: HTTPAuthorizationCredentials | None = Depends(_bearer),
    session: AsyncSession = Depends(get_db),
    auth: AuthService = Depends(get_auth_service),
) -> UserContext:
    """Accepte un JWT org OU une clé API agent (header X-Agent-Key ou Bearer agt_...)."""

    # Clé API dédiée agent (X-Agent-Key ou Bearer agt_...)
    raw_key: str | None = request.headers.get("x-agent-key")
    if not raw_key and credentials and credentials.credentials.startswith("agt_"):
        raw_key = credentials.credentials

    if raw_key:
        from agent_creator.services.agent_security import lookup_api_key

        db_store = DbStore(session)
        key = await lookup_api_key(db_store, raw_key)
        if not key:
            raise HTTPException(status_code=401, detail="Clé API agent invalide ou révoquée.")
        agent = await db_store.get_published_agent(key.agent_id)
        if not agent:
            raise HTTPException(status_code=404, detail="Agent introuvable.")
        # Récupère l'org de l'agent pour construire un UserContext minimal
        user_ctx = await _context_from_org_id(session, auth, agent.organization_id)
        if user_ctx is None:
            raise HTTPException(status_code=401, detail="Organisation agent introuvable.")
        return user_ctx

    # JWT standard
    if not credentials or credentials.scheme.lower() != "bearer":
        raise HTTPException(status_code=status.HTTP_401_UNAUTHORIZED, detail="Authentification requise")
    try:
        user_id, org_id = auth.decode_token(credentials.credentials)
        user, org, membership = await auth.get_user_context(session, user_id, org_id)
    except AuthError as exc:
        raise HTTPException(status_code=status.HTTP_401_UNAUTHORIZED, detail=str(exc)) from exc
    return UserContext(user=user, organization=org, membership=membership)


async def _context_from_org_id(session: AsyncSession, auth: AuthService, org_id: str) -> UserContext | None:
    """Construit un UserContext minimal depuis un org_id (utilisé pour auth clé API)."""
    from agent_creator.db.repository import DbStore
    from agent_creator.models.user import Membership, User

    db_store = DbStore(session)
    org = await db_store.get_organization(org_id)
    if not org:
        return None
    dummy_user = User(id="api-key-consumer", email="api-key@agentia.internal", full_name="API Key Consumer")
    dummy_membership = Membership(id="api-key-membership", user_id="api-key-consumer", organization_id=org_id)
    return UserContext(user=dummy_user, organization=org, membership=dummy_membership)

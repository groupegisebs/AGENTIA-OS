from __future__ import annotations

from collections.abc import AsyncIterator
from dataclasses import dataclass

from fastapi import Depends, HTTPException, status
from fastapi.security import HTTPAuthorizationCredentials, HTTPBearer
from sqlalchemy.ext.asyncio import AsyncSession

from agent_creator.config import get_settings
from agent_creator.db.repository import DbStore
from agent_creator.db.session import async_session_factory
from agent_creator.models.organization import Organization
from agent_creator.models.user import Membership, User
from agent_creator.services.auth import AuthError, AuthService

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
    from agent_creator.main import billing_service
    from agent_creator.services.deployment import DeploymentService

    return DeploymentService(db, billing_service)

from collections.abc import AsyncIterator
from contextlib import asynccontextmanager

from sqlalchemy.ext.asyncio import AsyncSession, async_sessionmaker, create_async_engine

from agent_creator.config import get_settings
from agent_creator.db.base import Base
from agent_creator.db import tables  # noqa: F401

_engine = None
_async_session_factory: async_sessionmaker[AsyncSession] | None = None


def _normalize_database_url(url: str) -> str:
    if url.startswith("postgresql://"):
        return url.replace("postgresql://", "postgresql+asyncpg://", 1)
    if url.startswith("postgres://"):
        return url.replace("postgres://", "postgresql+asyncpg://", 1)
    return url


def get_engine():
    global _engine, _async_session_factory
    if _engine is None:
        settings = get_settings()
        db_url = _normalize_database_url(settings.database_url)
        _engine = create_async_engine(db_url, echo=False, future=True)
        _async_session_factory = async_sessionmaker(_engine, expire_on_commit=False)
    return _engine


def async_session_factory() -> async_sessionmaker[AsyncSession]:
    get_engine()
    assert _async_session_factory is not None
    return _async_session_factory


async def close_engine() -> None:
    """Dispose the global async engine and clear cached factory."""
    global _engine, _async_session_factory
    if _engine is not None:
        await _engine.dispose()
    _engine = None
    _async_session_factory = None


async def init_db() -> None:
    engine = get_engine()
    async with engine.begin() as conn:
        await conn.run_sync(Base.metadata.create_all)


@asynccontextmanager
async def get_session() -> AsyncIterator[AsyncSession]:
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

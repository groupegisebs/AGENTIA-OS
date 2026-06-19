import os

os.environ.setdefault("DATABASE_URL", "sqlite+aiosqlite:///:memory:")
os.environ.setdefault("JWT_SECRET", "test-secret-key-for-tests-only")

import pytest
from httpx import ASGITransport, AsyncClient

from agent_creator.config import get_settings

get_settings.cache_clear()


@pytest.fixture
def anyio_backend() -> str:
    return "asyncio"


@pytest.fixture(autouse=True)
async def setup_database():
    import agent_creator.db.session as db_session

    db_session._engine = None
    db_session._async_session_factory = None
    from agent_creator.db.session import init_db

    await init_db()
    yield


@pytest.fixture
async def client():
    from agent_creator.main import app

    transport = ASGITransport(app=app)
    async with AsyncClient(transport=transport, base_url="http://test") as ac:
        yield ac

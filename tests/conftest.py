import os
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))

os.environ.setdefault("DATABASE_URL", "sqlite+aiosqlite:///:memory:")
os.environ.setdefault("JWT_SECRET", "test-secret-key-for-tests-only")

import pytest
import pytest_asyncio
from httpx import ASGITransport, AsyncClient

from agent_creator.config import get_settings

get_settings.cache_clear()


@pytest.fixture
def anyio_backend() -> str:
    return "asyncio"


@pytest_asyncio.fixture(autouse=True)
async def setup_database():
    from agent_creator.db.session import close_engine, init_db

    await close_engine()
    await init_db()
    yield
    await close_engine()


@pytest_asyncio.fixture
async def client():
    from agent_creator.main import app

    transport = ASGITransport(app=app)
    async with AsyncClient(transport=transport, base_url="http://test") as ac:
        yield ac

from contextlib import asynccontextmanager
from pathlib import Path
from typing import AsyncIterator

import redis.asyncio as aioredis
from arq import create_pool
from arq.connections import RedisSettings
from fastapi import FastAPI, HTTPException
from fastapi.middleware.cors import CORSMiddleware
from fastapi.responses import FileResponse
from fastapi.staticfiles import StaticFiles

from agent_creator import __version__
from agent_creator.config import get_settings
from agent_creator.db.session import init_db
from agent_creator.routers import architect, auth, billing, conversations, organizations, os_runtime, plans
from agent_creator.routers.agents import marketplace_router, router as agents_router
from agent_creator.routers.publishing import router as publishing_router
from agent_creator.services.billing import BillingService
from agent_creator.services.blueprint_generator import BlueprintGenerator
from agent_creator.services.cache import AgentCache
from agent_creator.services.extractor import RequirementExtractor
from agent_creator.services.llm import LLMService
from agent_creator.services.payment import create_payment_provider
from agent_creator.services.os_foundation import AgentEventBus, AgentOSService

settings = get_settings()
payment_provider = create_payment_provider(settings)
billing_service = BillingService(settings, payment_provider)
llm_service = LLMService(settings)
llm = llm_service  # backward-compat alias
extractor = RequirementExtractor(llm_service)
blueprint_generator = BlueprintGenerator(extractor)
event_bus = AgentEventBus()
agent_os_service = AgentOSService(event_bus)

STATIC_DIR = Path(__file__).resolve().parent / "static"
SPA_INDEX = STATIC_DIR / "index.html"

# Préfixes réservés à l'API — ne pas servir index.html pour ces chemins GET
API_PATH_PREFIXES = (
    "auth/",
    "conversations",
    "organizations",
    "billing",
    "plans/",
    "architect/",
    "agents",
    "os/",
    "marketplace/",
    "publishing/",
    "health",
)


@asynccontextmanager
async def lifespan(application: FastAPI) -> AsyncIterator[None]:
    Path("data").mkdir(exist_ok=True)
    await init_db()

    # ── Redis + cache manifestes agents ──────────────────────────────────────
    redis_client = aioredis.from_url(
        settings.redis_url, encoding="utf-8", decode_responses=True
    )
    cache = AgentCache(redis_client)
    redis_ok = await cache.ping()

    # ── Pool ARQ pour l'enqueue des runs asynchrones ──────────────────────────
    arq_pool = None
    if redis_ok:
        try:
            arq_pool = await create_pool(RedisSettings.from_dsn(settings.redis_url))
        except Exception as exc:
            print(f"::warning::ARQ pool non disponible : {exc}")

    application.state.cache = cache
    application.state.arq_pool = arq_pool
    application.state.agent_os_service = agent_os_service

    llm_mode = llm.mode_label
    pay_mode = payment_provider.provider_name
    db_scheme = settings.database_url.split("://", 1)[0]
    redis_status = "OK" if redis_ok else "indisponible (mode sync uniquement)"
    print(
        f"Agent Creator démarré — LLM : {llm_mode} — paiement : {pay_mode}"
        f" — DB : {db_scheme} — Redis : {redis_status}"
    )
    if SPA_INDEX.is_file():
        print(f"Interface web : {STATIC_DIR}")
    else:
        print(f"::warning::SPA introuvable : {SPA_INDEX}")

    yield

    await cache.close()
    if arq_pool:
        await arq_pool.aclose()


app = FastAPI(
    title="Agentia Factory — Agent Creator",
    description=(
        "Agentia Factory — SaaS avec comptes, abonnement et facturation au déploiement. "
        "Dialogue naturel → blueprint (gratuit) → déploiement (facturable)."
    ),
    version=__version__,
    lifespan=lifespan,
    docs_url="/docs",
    redoc_url="/redoc",
)

app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_methods=["*"],
    allow_headers=["*"],
)

app.include_router(auth.router)
app.include_router(billing.router)
app.include_router(conversations.router)
app.include_router(plans.router)
app.include_router(organizations.router)
app.include_router(architect.router)
app.include_router(agents_router)
app.include_router(marketplace_router)
app.include_router(os_runtime.router)
app.include_router(publishing_router)


def _spa_index() -> FileResponse:
    if not SPA_INDEX.is_file():
        raise HTTPException(status_code=404, detail="Interface web non disponible")
    return FileResponse(SPA_INDEX)


def _is_api_path(path: str) -> bool:
    normalized = path.lstrip("/")
    if not normalized:
        return False
    if normalized in ("health", "openapi.json"):
        return True
    return normalized.startswith(API_PATH_PREFIXES)


if STATIC_DIR.is_dir():
    app.mount("/static", StaticFiles(directory=STATIC_DIR), name="static")


@app.get("/")
@app.get("/workspace")
@app.get("/cockpit")
@app.get("/marketplace")
@app.get("/architect")
@app.get("/inscription")
@app.get("/connexion")
@app.get("/connexion/oauth")
@app.get("/mon-compte")
@app.get("/abonnement")
@app.get("/documentation")
@app.get("/paiement/succes")
@app.get("/paiement/annule")
async def spa_sections() -> FileResponse:
    return _spa_index()


@app.get("/solution/{conversation_id}")
@app.get("/editor/{conversation_id}")
@app.get("/composer/{conversation_id}")
async def spa_with_id(conversation_id: str) -> FileResponse:
    return _spa_index()


@app.get("/health")
async def health() -> dict[str, str | bool]:
    redis_ok = await app.state.cache.ping() if hasattr(app.state, "cache") else False
    return {
        "status": "ok",
        "service": "agent-creator",
        "version": __version__,
        "llm_mode": llm.mode_label,
        "payment_provider": payment_provider.provider_name,
        "redis": "ok" if redis_ok else "unavailable",
        "async_workers": app.state.arq_pool is not None if hasattr(app.state, "arq_pool") else False,
        "spa_index": str(SPA_INDEX),
        "spa_ready": SPA_INDEX.is_file(),
    }


@app.get("/{full_path:path}")
async def spa_fallback(full_path: str) -> FileResponse:
    """Refresh navigateur sur une route SPA non listée explicitement."""
    if _is_api_path(full_path):
        raise HTTPException(status_code=404, detail="Not Found")
    if full_path.startswith("docs") or full_path.startswith("redoc"):
        raise HTTPException(status_code=404, detail="Not Found")
    return _spa_index()


def run() -> None:
    import uvicorn

    uvicorn.run(
        "agent_creator.main:app",
        host=settings.host,
        port=settings.port,
        reload=False,
    )


if __name__ == "__main__":
    run()

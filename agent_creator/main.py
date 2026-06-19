from contextlib import asynccontextmanager
from pathlib import Path
from typing import AsyncIterator

from fastapi import FastAPI, HTTPException
from fastapi.middleware.cors import CORSMiddleware
from fastapi.responses import FileResponse
from fastapi.staticfiles import StaticFiles

from agent_creator import __version__
from agent_creator.config import get_settings
from agent_creator.db.session import init_db
from agent_creator.routers import architect, auth, billing, conversations, organizations, plans
from agent_creator.services.billing import BillingService
from agent_creator.services.blueprint_generator import BlueprintGenerator
from agent_creator.services.extractor import RequirementExtractor
from agent_creator.services.llm import LLMService
from agent_creator.services.payment import create_payment_provider

settings = get_settings()
payment_provider = create_payment_provider(settings)
billing_service = BillingService(settings, payment_provider)
llm = LLMService(settings)
extractor = RequirementExtractor(llm)
blueprint_generator = BlueprintGenerator(extractor)

STATIC_DIR = Path(__file__).resolve().parent / "static"
SPA_INDEX = STATIC_DIR / "index.html"

# Préfixes réservés à l'API — ne pas servir index.html pour ces chemins GET
API_PATH_PREFIXES = (
    "auth/",
    "conversations",
    "organizations",
    "billing",
    "plans",
    "architect/",
    "health",
    "openapi.json",
    "redoc",
    "static/",
)

SPA_EXACT_PATHS = frozenset({
    "/",
    "/workspace",
    "/cockpit",
    "/marketplace",
    "/architect",
    "/inscription",
    "/connexion",
    "/mon-compte",
    "/abonnement",
    "/documentation",
    "/paiement/succes",
    "/paiement/annule",
})


@asynccontextmanager
async def lifespan(_: FastAPI) -> AsyncIterator[None]:
    Path("data").mkdir(exist_ok=True)
    await init_db()
    llm_mode = llm.mode_label
    pay_mode = payment_provider.provider_name
    db_scheme = settings.database_url.split("://", 1)[0]
    print(f"Agent Creator démarré — LLM : {llm_mode} — paiement : {pay_mode} — DB : {db_scheme}")
    if SPA_INDEX.is_file():
        print(f"Interface web : {STATIC_DIR}")
    else:
        print(f"::warning::SPA introuvable : {SPA_INDEX}")
    yield


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


def _spa_index() -> FileResponse:
    if not SPA_INDEX.is_file():
        raise HTTPException(status_code=404, detail="Interface web non disponible")
    return FileResponse(SPA_INDEX)


def _is_api_path(path: str) -> bool:
    normalized = path.lstrip("/")
    if not normalized:
        return False
    return any(normalized.startswith(prefix.rstrip("/")) for prefix in API_PATH_PREFIXES if prefix != "health") or normalized == "health"


if STATIC_DIR.is_dir():
    app.mount("/static", StaticFiles(directory=STATIC_DIR), name="static")

if SPA_INDEX.is_file():

    @app.get("/")
    @app.get("/workspace")
    @app.get("/cockpit")
    @app.get("/marketplace")
    @app.get("/architect")
    @app.get("/inscription")
    @app.get("/connexion")
    @app.get("/mon-compte")
    @app.get("/abonnement")
    @app.get("/documentation")
    @app.get("/paiement/succes")
    @app.get("/paiement/annule")
    async def spa_sections() -> FileResponse:
        return _spa_index()

    @app.get("/solution/{conversation_id}")
    @app.get("/editor/{conversation_id}")
    async def spa_with_id(conversation_id: str) -> FileResponse:
        return _spa_index()

    @app.get("/{full_path:path}")
    async def spa_fallback(full_path: str) -> FileResponse:
        """Routes SPA non listées explicitement (refresh navigateur sur une URL client)."""
        if _is_api_path(full_path):
            raise HTTPException(status_code=404, detail="Not Found")
        if full_path.startswith("docs") or full_path.startswith("redoc"):
            raise HTTPException(status_code=404, detail="Not Found")
        return _spa_index()


@app.get("/health")
async def health() -> dict[str, str]:
    return {
        "status": "ok",
        "service": "agent-creator",
        "version": __version__,
        "llm_mode": llm.mode_label,
        "payment_provider": payment_provider.provider_name,
    }


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

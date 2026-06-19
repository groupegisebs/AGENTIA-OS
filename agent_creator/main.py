from contextlib import asynccontextmanager
from pathlib import Path
from typing import AsyncIterator

from fastapi import FastAPI
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


@asynccontextmanager
async def lifespan(_: FastAPI) -> AsyncIterator[None]:
    Path("data").mkdir(exist_ok=True)
    await init_db()
    llm_mode = llm.mode_label
    pay_mode = payment_provider.provider_name
    db_scheme = settings.database_url.split("://", 1)[0]
    print(f"Agent Creator démarré — LLM : {llm_mode} — paiement : {pay_mode} — DB : {db_scheme}")
    yield


app = FastAPI(
    title="Agentia Factory — Agent Creator",
    description=(
        "Agentia Factory — SaaS avec comptes, abonnement et facturation au déploiement. "
        "Dialogue naturel → blueprint (gratuit) → déploiement (facturable)."
    ),
    version=__version__,
    lifespan=lifespan,
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

STATIC_DIR = Path(__file__).parent / "static"

if STATIC_DIR.is_dir():
    app.mount("/static", StaticFiles(directory=STATIC_DIR), name="static")

    def _spa_index() -> FileResponse:
        return FileResponse(STATIC_DIR / "index.html")

    @app.get("/")
    @app.get("/workspace")
    @app.get("/cockpit")
    @app.get("/marketplace")
    @app.get("/architect")
    @app.get("/inscription")
    @app.get("/connexion")
    @app.get("/mon-compte")
    @app.get("/abonnement")
    @app.get("/paiement/succes")
    @app.get("/paiement/annule")
    async def spa_sections() -> FileResponse:
        return _spa_index()

    @app.get("/solution/{conversation_id}")
    @app.get("/editor/{conversation_id}")
    async def spa_with_id(conversation_id: str) -> FileResponse:
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

from functools import lru_cache

from pydantic_settings import BaseSettings, SettingsConfigDict

from agent_creator.models.subscription import SubscriptionPlan


class Settings(BaseSettings):
    model_config = SettingsConfigDict(env_file=".env", env_file_encoding="utf-8", extra="ignore")

    openai_api_key: str | None = None
    openai_base_url: str = "https://api.openai.com/v1"
    openai_model: str = "gpt-4o-mini"

    # Google Gemini (prioritaire en production si LLM_PROVIDER=gemini ou clé seule)
    gemini_api_key: str | None = None
    gemini_model: str = "gemini-2.0-flash"
    llm_provider: str = "auto"  # auto | gemini | openai | mock

    host: str = "0.0.0.0"
    port: int = 8000

    # Base de données — production : PostgreSQL via secret GHA AGENTIA_OS_DATABASE_URL
    database_url: str = "sqlite+aiosqlite:///./data/agentia.db"

    # Redis — broker ARQ + cache manifestes agents (dormance)
    redis_url: str = "redis://127.0.0.1:6379"

    # Authentification JWT — en production : secret GHA obligatoire
    jwt_secret: str = "dev-only-change-in-production"
    jwt_algorithm: str = "HS256"
    jwt_expire_minutes: int = 60 * 24 * 7

    # OAuth2 — redirect URI = {oauth_redirect_base_url}/auth/oauth/{provider}/callback
    oauth_redirect_base_url: str = "http://localhost:8000"
    oauth_google_client_id: str = ""
    oauth_google_client_secret: str = ""
    oauth_facebook_app_id: str = ""
    oauth_facebook_app_secret: str = ""
    oauth_github_client_id: str = ""
    oauth_github_client_secret: str = ""
    oauth_microsoft_client_id: str = ""
    oauth_microsoft_client_secret: str = ""
    oauth_microsoft_tenant_id: str = "common"

    # Facturation SaaS (devise : EUR)
    default_organization_id: str = "org-demo-0001"
    default_organization_name: str = "Organisation démo"
    deployment_base_fee: float = 0.0
    deployment_complexity_multiplier: float = 0.15
    billing_currency: str = "EUR"

    # GiseBsPayGateway (laisser URL ou clé vide → mock)
    gisebs_pay_gateway_url: str = ""
    gisebs_pay_app_code: str = "AGENTIAOS"
    gisebs_pay_api_key: str = ""
    gisebs_pay_success_url: str = "http://localhost:8000/paiement/succes"
    gisebs_pay_cancel_url: str = "http://localhost:8000/paiement/annule"
    gisebs_pay_deployment_product_code: str = "AGENT-DEPLOY"
    gisebs_pay_deployment_plan_code: str = "DEPLOY-M"
    gisebs_pay_deploy_plan_small: str = "DEPLOY-S"
    gisebs_pay_deploy_plan_medium: str = "DEPLOY-M"
    gisebs_pay_deploy_plan_large: str = "DEPLOY-L"
    gisebs_pay_subscription_product_code: str = "AGENT-SUB"
    gisebs_pay_plan_professional: str = "MONTHLY-PRO"
    gisebs_pay_plan_business: str = "MONTHLY-BUSINESS"
    gisebs_pay_plan_enterprise: str = "MONTHLY-ENTERPRISE"
    gisebs_pay_poll_attempts: int = 0
    gisebs_pay_poll_delay_seconds: float = 2.0
    gisebs_pay_request_timeout_seconds: float = 30.0

    @property
    def gemini_enabled(self) -> bool:
        return bool(self.gemini_api_key and self.gemini_api_key.strip())

    @property
    def openai_enabled(self) -> bool:
        return bool(self.openai_api_key and self.openai_api_key.strip())

    @property
    def llm_enabled(self) -> bool:
        provider = (self.llm_provider or "auto").lower()
        if provider == "mock":
            return False
        if provider == "gemini":
            return self.gemini_enabled
        if provider == "openai":
            return self.openai_enabled
        return self.gemini_enabled or self.openai_enabled

    @property
    def active_llm_provider(self) -> str:
        provider = (self.llm_provider or "auto").lower()
        if provider == "mock" or not self.llm_enabled:
            return "mock"
        if provider == "openai" and self.openai_enabled:
            return "openai"
        if provider == "gemini" and self.gemini_enabled:
            return "gemini"
        if provider == "auto":
            if self.gemini_enabled:
                return "gemini"
            if self.openai_enabled:
                return "openai"
        return "mock"

    @property
    def is_postgresql(self) -> bool:
        url = self.database_url.lower()
        return url.startswith(("postgresql+asyncpg://", "postgresql://", "postgres://"))

    @property
    def is_production_secrets_ok(self) -> bool:
        """True si JWT fort et DATABASE_URL PostgreSQL (secret GHA)."""
        return self.jwt_secret != "dev-only-change-in-production" and self.is_postgresql

    # GISEMailSender — envoi d'emails transactionnels (reset password, notifications)
    gisemailsender_url: str = ""
    gisemailsender_api_key: str = ""
    gisemailsender_client_code: str = "AGENTIAOS"
    gisemailsender_from_name: str = "Agentia OS"
    app_base_url: str = "http://localhost:8000"

    # GISEBoutique Partner API (Publishing Center)
    giseboutique_partner_url: str = ""
    giseboutique_partner_api_key: str = ""

    @property
    def email_configured(self) -> bool:
        return bool(
            self.gisemailsender_url.strip()
            and self.gisemailsender_api_key.strip()
            and self.gisemailsender_client_code.strip()
        )

    @property
    def giseboutique_partner_configured(self) -> bool:
        return bool(self.giseboutique_partner_url.strip() and self.giseboutique_partner_api_key.strip())

    @property
    def gisebs_pay_configured(self) -> bool:
        return bool(
            self.gisebs_pay_gateway_url.strip()
            and self.gisebs_pay_app_code.strip()
            and self.gisebs_pay_api_key.strip()
        )

    def gisebs_pay_plan_code_for(self, plan: SubscriptionPlan) -> str | None:
        mapping = {
            SubscriptionPlan.PROFESSIONAL: self.gisebs_pay_plan_professional,
            SubscriptionPlan.BUSINESS: self.gisebs_pay_plan_business,
            SubscriptionPlan.ENTERPRISE: self.gisebs_pay_plan_enterprise,
        }
        code = mapping.get(plan, "")
        return code.strip() or None


@lru_cache
def get_settings() -> Settings:
    from agent_creator.secrets_loader import load_server_secret_overrides

    settings = Settings()
    overrides = load_server_secret_overrides()
    if overrides:
        settings = settings.model_copy(update=overrides)
    return settings

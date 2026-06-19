from functools import lru_cache

from pydantic_settings import BaseSettings, SettingsConfigDict

from agent_creator.models.subscription import SubscriptionPlan


class Settings(BaseSettings):
    model_config = SettingsConfigDict(env_file=".env", env_file_encoding="utf-8", extra="ignore")

    openai_api_key: str | None = None
    openai_base_url: str = "https://api.openai.com/v1"
    openai_model: str = "gpt-4o-mini"
    host: str = "0.0.0.0"
    port: int = 8000

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
    gisebs_pay_deployment_plan_code: str = "ONE-TIME"
    gisebs_pay_subscription_product_code: str = "AGENT-SUB"
    gisebs_pay_plan_professional: str = "MONTHLY-PRO"
    gisebs_pay_plan_business: str = "MONTHLY-BUSINESS"
    gisebs_pay_plan_enterprise: str = "MONTHLY-ENTERPRISE"
    gisebs_pay_poll_attempts: int = 0
    gisebs_pay_poll_delay_seconds: float = 2.0
    gisebs_pay_request_timeout_seconds: float = 30.0

    @property
    def llm_enabled(self) -> bool:
        return bool(self.openai_api_key and self.openai_api_key.strip())

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
    return Settings()

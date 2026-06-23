"""Estimation temps réel pour le workspace (complexité, coût, ROI)."""

from agent_creator.models.blueprint import Blueprint
from agent_creator.models.conversation import Conversation
from agent_creator.models.organization import Organization
from agent_creator.services.billing import BillingService
from agent_creator.services.plans import get_plan_config


def complexity_label(score: float) -> str:
    if score <= 1.2:
        return "Faible"
    if score <= 1.8:
        return "Moyenne"
    return "Élevée"


def estimate_build_time_min(blueprint: Blueprint | None, message_count: int) -> int:
    if blueprint:
        base = 8 + len(blueprint.components) * 4
        if blueprint.solution_type.value in ("hybrid", "microservice"):
            base += 6
        return min(base, 45)
    return max(12, 10 + message_count * 3)


def estimate_hours_saved(blueprint: Blueprint | None, conversation: Conversation) -> float:
    if blueprint and blueprint.requirements.objectives:
        return round(len(blueprint.requirements.objectives) * 12 + len(blueprint.components) * 4, 1)
    user_text = " ".join(m.content.lower() for m in conversation.user_messages)
    if any(k in user_text for k in ("email", "facture", "prospect", "rh", "dépense")):
        return 24.0
    return max(8.0, len(conversation.user_messages) * 6.0)


def estimate_monthly_cost(
    blueprint: Blueprint | None,
    organization: Organization,
    billing: BillingService,
) -> float:
    plan = get_plan_config(organization.plan)
    base = plan.monthly_price_eur
    if blueprint:
        complexity = billing.calculate_complexity_score(blueprint)
        return round(base + complexity * 18, 0)
    return round(base + 29, 0)


def estimate_roi_percent(hours_saved: float, monthly_cost: float) -> int:
    if monthly_cost <= 0:
        return 0
    hourly_value = 45
    monthly_savings = hours_saved * hourly_value
    if monthly_savings <= 0:
        return 0
    return int(round((monthly_savings - monthly_cost) / monthly_cost * 100))


def build_estimates(
    conversation: Conversation,
    blueprint: Blueprint | None,
    organization: Organization,
    billing: BillingService,
) -> dict:
    complexity_score = billing.calculate_complexity_score(blueprint) if blueprint else 1.0
    hours_saved = estimate_hours_saved(blueprint, conversation)
    monthly_cost = estimate_monthly_cost(blueprint, organization, billing)
    return {
        "complexity": complexity_label(complexity_score),
        "complexity_score": complexity_score,
        "build_time_min": estimate_build_time_min(blueprint, len(conversation.user_messages)),
        "monthly_cost_eur": monthly_cost,
        "hours_saved_per_month": hours_saved,
        "roi_percent": estimate_roi_percent(hours_saved, monthly_cost),
        "ready": blueprint is not None or len(conversation.user_messages) >= 2,
    }

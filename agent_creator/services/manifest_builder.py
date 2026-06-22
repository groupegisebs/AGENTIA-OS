"""Construit un AgentManifest exécutable depuis un Blueprint."""

from agent_creator.config import Settings
from agent_creator.models.agent import AgentManifest, AgentPolicies
from agent_creator.models.blueprint import Blueprint
from agent_creator.models.requirement import SolutionType
from agent_creator.models.subscription import SubscriptionPlan

_DOMAIN_CATEGORY: dict[str, str] = {
    "comptab": "Comptabilité",
    "cabinet": "Comptabilité",
    "facture": "Finance",
    "rh": "RH",
    "recrutement": "RH",
    "employé": "RH",
    "crm": "CRM",
    "prospect": "CRM",
    "commercial": "CRM",
    "vente": "CRM",
    "jurid": "Juridique",
    "contrat": "Juridique",
    "formation": "Éducation",
    "école": "Éducation",
    "étudiant": "Éducation",
    "finance": "Finance",
    "dépense": "Finance",
    "budget": "Finance",
}

_PLAN_LIMITS: dict[SubscriptionPlan, dict] = {
    SubscriptionPlan.FREE: {"max_input_chars": 2000, "max_requests_per_hour": 20},
    SubscriptionPlan.PROFESSIONAL: {"max_input_chars": 4000, "max_requests_per_hour": 60},
    SubscriptionPlan.BUSINESS: {"max_input_chars": 8000, "max_requests_per_hour": 200},
    SubscriptionPlan.ENTERPRISE: {"max_input_chars": 16000, "max_requests_per_hour": 1000},
}


def _infer_category(blueprint: Blueprint) -> str:
    domain = (blueprint.requirements.domain or "").lower()
    text = domain + " " + " ".join(blueprint.requirements.objectives).lower()
    for keyword, category in _DOMAIN_CATEGORY.items():
        if keyword in text:
            return category
    type_map = {
        SolutionType.AGENT: "Assistant IA",
        SolutionType.HYBRID: "Solution hybride",
        SolutionType.WORKFLOW: "Automatisation",
        SolutionType.API: "Intégration",
        SolutionType.MICROSERVICE: "Service technique",
    }
    return type_map.get(blueprint.solution_type, "Général")


def _build_system_prompt(blueprint: Blueprint) -> str:
    req = blueprint.requirements
    parts: list[str] = [
        f"Tu es un assistant métier spécialisé — solution : {blueprint.title}.",
    ]
    if req.domain:
        parts.append(f"Domaine : {req.domain}.")
    if req.objectives:
        parts.append("Objectifs : " + "; ".join(req.objectives[:5]) + ".")
    if req.constraints:
        parts.append("Contraintes : " + "; ".join(req.constraints[:3]) + ".")
    parts.append(
        "Réponds en français. Sois précis, concis et orienté résultat. "
        "Ne divulgue pas d'informations confidentielles ni de données personnelles."
    )
    return " ".join(parts)


def build_manifest(blueprint: Blueprint, plan: SubscriptionPlan, settings: Settings) -> AgentManifest:
    limits = _PLAN_LIMITS.get(plan, _PLAN_LIMITS[SubscriptionPlan.FREE])
    return AgentManifest(
        system_prompt=_build_system_prompt(blueprint),
        llm_provider=settings.active_llm_provider,
        model=settings.gemini_model if settings.active_llm_provider == "gemini" else settings.openai_model,
        allowed_tools=[],
        policies=AgentPolicies(
            max_input_chars=limits["max_input_chars"],
            max_requests_per_hour=limits["max_requests_per_hour"],
            pii_filter=True,
        ),
        components=[
            {"name": c.name, "type": c.type, "description": c.description}
            for c in blueprint.components
        ],
        category=_infer_category(blueprint),
        domain=blueprint.requirements.domain,
    )

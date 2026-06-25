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
    """Génère un system prompt détaillé pour l'agent déployé (mode sans LLM)."""
    req = blueprint.requirements
    parts: list[str] = []

    parts.append(
        f"Tu es un agent IA spécialisé — {blueprint.title}."
    )

    if req.domain:
        parts.append(f"Ton domaine d'expertise est : {req.domain}.")

    if req.objectives:
        parts.append(
            "Tes missions principales :\n"
            + "\n".join(f"- {obj}" for obj in req.objectives[:6])
        )

    if req.constraints:
        parts.append(
            "Contraintes à respecter impérativement :\n"
            + "\n".join(f"- {c}" for c in req.constraints[:4])
        )

    if req.data_sources:
        parts.append(
            f"Tu travailles avec les sources de données suivantes : {', '.join(req.data_sources[:5])}."
        )

    component_names = [c.name for c in blueprint.components]
    if component_names:
        parts.append(
            f"L'architecture comprend les composants suivants : {', '.join(component_names)}."
        )

    if blueprint.data_flow:
        parts.append(
            "Processus d'exécution :\n"
            + "\n".join(blueprint.data_flow[:7])
        )

    parts.append(
        "\nRègles de comportement :\n"
        "- Réponds toujours en français, de façon précise et professionnelle\n"
        "- Reste strictement dans ton domaine de compétence défini\n"
        "- Pour chaque demande, identifie d'abord les données nécessaires avant d'agir\n"
        "- Signale clairement si une action dépasse tes capacités ou nécessite une validation humaine\n"
        "- Ne divulgue jamais de données confidentielles ou personnelles\n"
        "- En cas d'ambiguïté, pose une question de clarification avant d'exécuter"
    )

    return "\n\n".join(parts)


async def _llm_system_prompt(blueprint: Blueprint, llm: object) -> str:
    """Génère un system prompt riche via LLM."""
    req = blueprint.requirements

    messages = [
        {
            "role": "system",
            "content": (
                "Tu es un expert en ingénierie de prompts pour agents IA professionnels. "
                "Génère un system prompt complet et précis pour un agent IA métier. "
                "Le system prompt doit :\n"
                "1. Définir clairement le rôle et les responsabilités de l'agent\n"
                "2. Lister les tâches concrètes qu'il sait exécuter\n"
                "3. Préciser les outils et systèmes avec lesquels il interagit\n"
                "4. Indiquer les règles métier et contraintes à respecter\n"
                "5. Définir le comportement en cas d'erreur ou d'ambiguïté\n"
                "6. Établir le ton et le style de communication\n\n"
                "Sois précis et opérationnel. Maximum 600 mots. En français uniquement."
            ),
        },
        {
            "role": "user",
            "content": (
                f"Génère le system prompt pour cet agent IA :\n\n"
                f"**Titre** : {blueprint.title}\n"
                f"**Domaine** : {req.domain or 'Non spécifié'}\n"
                f"**Objectifs** :\n"
                + "\n".join(f"- {o}" for o in req.objectives)
                + f"\n\n**Contraintes** :\n"
                + "\n".join(f"- {c}" for c in req.constraints)
                + f"\n\n**Sources de données** : {', '.join(req.data_sources)}\n"
                f"**Type de solution** : {blueprint.solution_type.value}\n"
                f"**Composants** : {', '.join(c.name for c in blueprint.components)}\n\n"
                f"**Flux de données** :\n"
                + "\n".join(blueprint.data_flow)
            ),
        },
    ]

    result = await llm.chat(messages)  # type: ignore[attr-defined]
    return result if result.strip() else _build_system_prompt(blueprint)


async def build_manifest(
    blueprint: Blueprint,
    plan: SubscriptionPlan,
    settings: Settings,
    llm: object | None = None,
) -> AgentManifest:
    limits = _PLAN_LIMITS.get(plan, _PLAN_LIMITS[SubscriptionPlan.FREE])

    if llm is not None:
        from agent_creator.services.llm import LLMService  # avoid circular at module level

        if isinstance(llm, LLMService) and not llm.is_mock_mode:
            system_prompt = await _llm_system_prompt(blueprint, llm)
        else:
            system_prompt = _build_system_prompt(blueprint)
    else:
        system_prompt = _build_system_prompt(blueprint)

    return AgentManifest(
        system_prompt=system_prompt,
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

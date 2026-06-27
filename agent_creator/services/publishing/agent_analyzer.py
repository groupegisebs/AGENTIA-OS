"""Étape 1 — Analyse automatique d'un agent publié pour extraire ses caractéristiques produit."""
from __future__ import annotations

import json

from agent_creator.models.agent import AgentManifest, PublishedAgent
from agent_creator.models.publishing import AgentAnalysis


_TOOL_LABELS: dict[str, str] = {
    "web_search": "Recherche web",
    "web_scraping": "Extraction web",
    "code_interpreter": "Interpréteur de code",
    "file_reader": "Lecture de fichiers",
    "email": "Email",
    "calendar": "Calendrier",
    "database": "Base de données",
    "api_call": "Appel API REST",
    "pdf_parser": "Lecture PDF",
    "spreadsheet": "Tableur",
    "crm": "CRM",
    "slack": "Slack",
    "notion": "Notion",
    "github": "GitHub",
}


def _infer_complexity(manifest: AgentManifest) -> str:
    score = len(manifest.allowed_tools) + len(manifest.components) * 2
    if score <= 2:
        return "simple"
    if score <= 6:
        return "medium"
    return "complex"


def _extract_technologies(manifest: AgentManifest) -> list[str]:
    techs: list[str] = []
    provider = manifest.llm_provider.lower()
    if provider in ("gemini", "google"):
        techs.append("Google Gemini")
    elif provider == "openai":
        techs.append("OpenAI GPT")
    elif provider == "auto":
        techs.append("LLM (auto-sélectionné)")
    if manifest.model:
        techs.append(manifest.model)
    return list(dict.fromkeys(techs))


def _extract_connectors(components: list[dict]) -> list[str]:
    connectors: list[str] = []
    for comp in components:
        comp_type = comp.get("type", "").lower()
        name = comp.get("name", "")
        if comp_type in ("connector", "integration", "api"):
            connectors.append(name or comp_type)
        elif comp_type == "workflow":
            connectors.append(f"Workflow: {name}" if name else "Workflow")
    return [c for c in connectors if c]


def _infer_categories(manifest: AgentManifest, description: str) -> list[str]:
    cats: list[str] = []
    if manifest.category and manifest.category != "Général":
        cats.append(manifest.category)
    if manifest.domain:
        cats.append(manifest.domain)
    desc_lower = description.lower()
    keyword_map = {
        "Productivité": ["productivit", "automat", "tâche", "workflow"],
        "Analyse de données": ["analys", "données", "rapport", "statistique", "dashboard"],
        "Service client": ["client", "support", "ticket", "réponse"],
        "Marketing": ["marketing", "campagne", "email", "seo"],
        "Finance": ["comptab", "facture", "paiement", "finance"],
        "RH": ["ressources humaines", " rh ", "recrutement", "paie"],
        "Développement": ["code", "développ", "test", "git", "deploy"],
        "E-commerce": ["commande", "produit", "boutique", "vente"],
    }
    for cat, keywords in keyword_map.items():
        if any(kw in desc_lower for kw in keywords):
            cats.append(cat)
    return list(dict.fromkeys(cats)) or ["Agent IA"]


def analyze_agent(agent: PublishedAgent) -> AgentAnalysis:
    """Extrait automatiquement toutes les caractéristiques d'un agent pour la fiche produit."""
    manifest = agent.manifest
    tools = [_TOOL_LABELS.get(t, t) for t in manifest.allowed_tools]
    connectors = _extract_connectors(manifest.components)

    apis: list[str] = []
    workflows: list[str] = []
    permissions: list[str] = []

    for comp in manifest.components:
        comp_type = comp.get("type", "").lower()
        name = comp.get("name", comp.get("type", ""))
        if comp_type == "api":
            apis.append(name)
        elif comp_type == "workflow":
            workflows.append(name)
        elif comp_type == "permission":
            permissions.append(name)

    if manifest.policies.pii_filter:
        permissions.append("Filtrage PII activé")
    if manifest.policies.max_requests_per_hour:
        permissions.append(f"Limite {manifest.policies.max_requests_per_hour} req/heure")

    technologies = _extract_technologies(manifest)
    categories = _infer_categories(manifest, agent.description)
    complexity = _infer_complexity(manifest)

    system_words = len(manifest.system_prompt.split())
    memory_enabled = system_words > 200 or any(
        "mémoire" in c.get("type", "").lower() or "memory" in c.get("type", "").lower()
        for c in manifest.components
    )

    objective = _extract_objective(manifest.system_prompt, agent.description)

    return AgentAnalysis(
        agent_id=agent.id,
        name=agent.title,
        description=agent.description or manifest.system_prompt[:200],
        objective=objective,
        llm_providers=[manifest.llm_provider],
        tools=tools,
        connectors=connectors,
        apis=apis,
        workflows=workflows,
        permissions=permissions,
        memory_enabled=memory_enabled,
        dependencies=technologies,
        technologies=technologies,
        categories=categories,
        domain=manifest.domain,
        complexity_level=complexity,
    )


def _extract_objective(system_prompt: str, description: str) -> str:
    """Extrait l'objectif principal depuis le system prompt ou la description."""
    if description and len(description) > 20:
        sentences = description.split(".")
        return sentences[0].strip() + "." if sentences else description

    lines = [l.strip() for l in system_prompt.splitlines() if l.strip()]
    for line in lines[:5]:
        if len(line) > 30 and not line.startswith("#") and not line.startswith("-"):
            return line[:200]

    return system_prompt[:150] + "..." if len(system_prompt) > 150 else system_prompt

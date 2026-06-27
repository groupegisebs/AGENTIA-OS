"""Étape 2 — Génération du contenu commercial via LLM."""
from __future__ import annotations

import json
import re

from agent_creator.models.publishing import AgentAnalysis, GeneratedContent
from agent_creator.services.llm import LLMService


_GENERATION_SYSTEM_PROMPT = """Tu es un expert en marketing de produits SaaS et d'agents IA.
Tu génères du contenu commercial professionnel, convaincant et optimisé SEO.
Réponds UNIQUEMENT en JSON valide, sans aucun texte avant ou après.
Tout le contenu doit être en français sauf indication contraire."""


def _build_generation_prompt(analysis: AgentAnalysis) -> str:
    tools_str = ", ".join(analysis.tools[:10]) if analysis.tools else "aucun outil spécifique"
    connectors_str = ", ".join(analysis.connectors[:5]) if analysis.connectors else "aucun"
    cats_str = ", ".join(analysis.categories[:3]) if analysis.categories else "Agent IA"

    return f"""Génère une fiche produit commerciale complète pour cet agent IA :

NOM : {analysis.name}
DESCRIPTION : {analysis.description}
OBJECTIF : {analysis.objective}
OUTILS : {tools_str}
CONNECTEURS : {connectors_str}
LLM : {", ".join(analysis.llm_providers)}
COMPLEXITÉ : {analysis.complexity_level}
CATÉGORIES : {cats_str}

Génère exactement ce JSON (tous les champs sont obligatoires) :
{{
  "commercial_title": "titre accrocheur de max 60 chars",
  "short_description": "description courte de 1-2 phrases (max 160 chars), optimisée SEO",
  "long_description": "description longue de 3-5 paragraphes HTML (utilise <p>, <ul>, <li>, <strong>)",
  "pitch": "argumentaire commercial de 2-3 phrases percutantes",
  "use_cases": ["cas d'usage 1", "cas d'usage 2", "cas d'usage 3", "cas d'usage 4", "cas d'usage 5"],
  "target_audience": ["profil cible 1", "profil cible 2", "profil cible 3"],
  "benefits": ["bénéfice concret 1", "bénéfice concret 2", "bénéfice concret 3", "bénéfice 4"],
  "features": ["fonctionnalité 1", "fonctionnalité 2", "fonctionnalité 3", "fonctionnalité 4"],
  "faq": [
    {{"question": "Comment fonctionne l'agent ?", "answer": "réponse détaillée"}},
    {{"question": "Quels systèmes sont compatibles ?", "answer": "réponse"}},
    {{"question": "Y a-t-il une période d'essai ?", "answer": "réponse"}},
    {{"question": "Quel support est disponible ?", "answer": "réponse"}}
  ],
  "user_documentation": "guide utilisateur complet en Markdown (min 300 mots)",
  "installation_guide": "guide d'installation étape par étape en Markdown",
  "prerequisites": ["prérequis technique 1", "prérequis 2"],
  "seo_keywords": ["mot-clé 1", "mot-clé 2", "mot-clé 3", "mot-clé 4", "mot-clé 5", "mot-clé 6", "mot-clé 7", "mot-clé 8"],
  "meta_title": "titre SEO de max 60 chars incluant le nom du produit",
  "meta_description": "description méta de max 155 chars pour les moteurs de recherche",
  "schema_org_tags": {{"@type": "SoftwareApplication", "applicationCategory": "{cats_str.split(',')[0].strip()}"}},
  "categories": {json.dumps(analysis.categories[:3])},
  "tags": ["tag1", "tag2", "tag3", "tag4", "tag5"],
  "similar_products": []
}}"""


def _parse_generated_content(raw: str) -> GeneratedContent:
    """Parse la réponse JSON du LLM avec fallback robuste."""
    cleaned = raw.strip()

    json_match = re.search(r'\{[\s\S]*\}', cleaned)
    if json_match:
        cleaned = json_match.group(0)

    try:
        data = json.loads(cleaned)
        return GeneratedContent(**{k: v for k, v in data.items() if k in GeneratedContent.model_fields})
    except (json.JSONDecodeError, Exception):
        return GeneratedContent(
            commercial_title="Titre à compléter",
            short_description="Description à compléter",
            long_description="<p>Description longue à compléter.</p>",
            pitch="Argumentaire à compléter",
        )


async def generate_product_content(
    analysis: AgentAnalysis,
    llm: LLMService,
) -> GeneratedContent:
    """Génère le contenu commercial complet via LLM."""
    messages = [
        {"role": "system", "content": _GENERATION_SYSTEM_PROMPT},
        {"role": "user", "content": _build_generation_prompt(analysis)},
    ]

    try:
        raw = await llm.chat(messages)
        content = _parse_generated_content(raw)
    except Exception:
        content = GeneratedContent(
            commercial_title=analysis.name,
            short_description=analysis.description[:160] if analysis.description else "",
            long_description=f"<p>{analysis.description}</p>",
            pitch=analysis.objective,
            use_cases=analysis.categories[:3],
            target_audience=["Professionnels", "Entreprises"],
            benefits=["Automatisation des tâches répétitives", "Gain de temps", "Réduction des erreurs"],
            features=[t for t in analysis.tools[:4]],
            categories=analysis.categories,
            tags=analysis.categories[:5],
            seo_keywords=analysis.categories + analysis.tools[:3],
            meta_title=f"{analysis.name} — Agent IA",
            meta_description=analysis.description[:155] if analysis.description else "",
        )

    if not content.commercial_title:
        content.commercial_title = analysis.name
    if not content.meta_title:
        content.meta_title = f"{content.commercial_title[:50]} — Agent IA"

    return content

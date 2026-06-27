"""Étape 3 — Génération des médias produit via DALL-E (OpenAI)."""
from __future__ import annotations

import httpx

from agent_creator.config import Settings
from agent_creator.models.publishing import AgentAnalysis, GeneratedContent, GeneratedMedia


_BANNER_PROMPT_TEMPLATE = (
    "Professional SaaS product banner for an AI agent called '{title}'. "
    "Modern flat design, gradient background in deep blue and purple tones, "
    "abstract neural network patterns, clean typography space for title. "
    "16:9 ratio, marketing quality, no text in image."
)

_ICON_PROMPT_TEMPLATE = (
    "App icon for AI agent '{title}'. "
    "Minimalist flat design, single bold symbol representing {category}, "
    "white icon on gradient background (blue to purple). "
    "Square format, app store quality."
)


async def generate_media(
    analysis: AgentAnalysis,
    content: GeneratedContent,
    settings: Settings,
) -> GeneratedMedia:
    """Génère les médias produit via DALL-E si la clé OpenAI est disponible."""
    if not settings.openai_enabled:
        return GeneratedMedia(
            generation_status="skipped",
            error="Clé OpenAI non configurée — médias non générés.",
        )

    banner_url = await _generate_image(
        prompt=_BANNER_PROMPT_TEMPLATE.format(
            title=content.commercial_title or analysis.name
        ),
        size="1792x1024",
        settings=settings,
    )

    icon_url = await _generate_image(
        prompt=_ICON_PROMPT_TEMPLATE.format(
            title=content.commercial_title or analysis.name,
            category=analysis.categories[0] if analysis.categories else "AI",
        ),
        size="1024x1024",
        settings=settings,
    )

    screenshots: list[str] = []
    for i, use_case in enumerate(analysis.use_cases[:2] if hasattr(analysis, "use_cases") else []):
        url = await _generate_image(
            prompt=(
                f"Clean UI screenshot mockup showing '{use_case}' feature of AI agent '{analysis.name}'. "
                "Modern dashboard interface, light theme, professional software screenshot style."
            ),
            size="1792x1024",
            settings=settings,
        )
        if url:
            screenshots.append(url)

    errors = [u for u in [banner_url, icon_url] if u is None]
    status = "completed" if not errors else ("partial" if any([banner_url, icon_url]) else "failed")

    return GeneratedMedia(
        banner_url=banner_url,
        icon_url=icon_url,
        logo_url=icon_url,
        screenshots=screenshots,
        generation_status=status,
    )


async def _generate_image(prompt: str, size: str, settings: Settings) -> str | None:
    """Appelle DALL-E 3 pour générer une image et retourne l'URL."""
    try:
        async with httpx.AsyncClient(timeout=60.0) as client:
            response = await client.post(
                f"{settings.openai_base_url}/images/generations",
                headers={
                    "Authorization": f"Bearer {settings.openai_api_key}",
                    "Content-Type": "application/json",
                },
                json={
                    "model": "dall-e-3",
                    "prompt": prompt[:4000],
                    "n": 1,
                    "size": size,
                    "quality": "standard",
                    "response_format": "url",
                },
            )
            if response.status_code == 200:
                data = response.json()
                return data["data"][0]["url"]
    except Exception:
        pass
    return None

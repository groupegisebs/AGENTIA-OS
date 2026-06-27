"""Étape 4 — Calcul des scores de qualité du produit."""
from __future__ import annotations

from agent_creator.models.publishing import (
    AgentAnalysis,
    GeneratedContent,
    GeneratedMedia,
    QualityScores,
    SaleSettings,
)


def _score_seo(content: GeneratedContent) -> tuple[float, list[str]]:
    score = 0.0
    improvements: list[str] = []

    if content.meta_title and 30 <= len(content.meta_title) <= 60:
        score += 25
    else:
        improvements.append("Meta title : viser 30-60 caractères")

    if content.meta_description and 120 <= len(content.meta_description) <= 155:
        score += 25
    else:
        improvements.append("Meta description : viser 120-155 caractères")

    if len(content.seo_keywords) >= 5:
        score += 25
    else:
        improvements.append(f"Mots-clés SEO : ajouter au moins {5 - len(content.seo_keywords)} mot(s)-clé(s)")

    if len(content.tags) >= 3:
        score += 15
    else:
        improvements.append("Tags : ajouter au moins 3 tags")

    if content.schema_org_tags:
        score += 10

    return min(score, 100.0), improvements


def _score_documentation(content: GeneratedContent) -> tuple[float, list[str]]:
    score = 0.0
    improvements: list[str] = []

    doc_words = len(content.user_documentation.split()) if content.user_documentation else 0
    if doc_words >= 300:
        score += 30
    elif doc_words >= 100:
        score += 15
        improvements.append("Documentation utilisateur : développer davantage (min 300 mots)")
    else:
        improvements.append("Documentation utilisateur manquante ou trop courte")

    install_words = len(content.installation_guide.split()) if content.installation_guide else 0
    if install_words >= 100:
        score += 20
    else:
        improvements.append("Guide d'installation : ajouter des étapes détaillées")

    if len(content.faq) >= 4:
        score += 20
    else:
        improvements.append(f"FAQ : ajouter {4 - len(content.faq)} question(s) supplémentaire(s)")

    if len(content.prerequisites) >= 1:
        score += 15
    else:
        improvements.append("Prérequis : lister les dépendances nécessaires")

    if len(content.features) >= 4:
        score += 15
    else:
        improvements.append("Fonctionnalités : décrire au moins 4 fonctionnalités")

    return min(score, 100.0), improvements


def _score_commercial(content: GeneratedContent, settings: SaleSettings) -> tuple[float, list[str]]:
    score = 0.0
    improvements: list[str] = []

    if content.pitch and len(content.pitch) >= 100:
        score += 20
    else:
        improvements.append("Argumentaire commercial : développer le pitch (min 100 chars)")

    if len(content.use_cases) >= 5:
        score += 20
    elif len(content.use_cases) >= 3:
        score += 10
        improvements.append("Cas d'usage : ajouter des exemples concrets")
    else:
        improvements.append("Cas d'usage manquants")

    if len(content.benefits) >= 4:
        score += 20
    else:
        improvements.append("Bénéfices : lister au moins 4 avantages concrets")

    if len(content.target_audience) >= 2:
        score += 15
    else:
        improvements.append("Public cible : définir les profils d'utilisateurs")

    if content.long_description and len(content.long_description) >= 300:
        score += 15
    else:
        improvements.append("Description longue : développer (min 300 chars)")

    if settings.pricing_model.value != "free" and settings.price > 0:
        score += 10
    elif settings.pricing_model.value == "free":
        score += 5

    return min(score, 100.0), improvements


def _score_security(analysis: AgentAnalysis) -> tuple[float, list[str]]:
    score = 50.0
    improvements: list[str] = []

    if analysis.permissions:
        score += 20
    else:
        improvements.append("Documenter les permissions requises par l'agent")

    from agent_creator.models.agent import AgentPolicies
    score += 15
    if not analysis.memory_enabled:
        score += 15
    else:
        improvements.append("Clarifier la politique de rétention des données en mémoire")

    return min(score, 100.0), improvements


def _score_completeness(
    content: GeneratedContent,
    media: GeneratedMedia | None,
    settings: SaleSettings,
) -> tuple[float, list[str]]:
    score = 0.0
    improvements: list[str] = []
    total_checks = 10

    checks = [
        (bool(content.commercial_title), "Titre commercial"),
        (bool(content.short_description), "Description courte"),
        (bool(content.long_description), "Description longue"),
        (bool(content.pitch), "Argumentaire"),
        (len(content.features) >= 3, "Fonctionnalités (min 3)"),
        (len(content.faq) >= 2, "FAQ (min 2 questions)"),
        (bool(content.user_documentation), "Documentation utilisateur"),
        (bool(settings.category), "Catégorie définie"),
        (media is not None and bool(media.banner_url), "Bannière produit"),
        (media is not None and bool(media.icon_url), "Icône produit"),
    ]

    for passed, label in checks:
        if passed:
            score += 100.0 / total_checks
        else:
            improvements.append(f"Manquant : {label}")

    return min(score, 100.0), improvements


def calculate_scores(
    analysis: AgentAnalysis,
    content: GeneratedContent,
    media: GeneratedMedia | None,
    settings: SaleSettings,
) -> QualityScores:
    seo_score, seo_imp = _score_seo(content)
    doc_score, doc_imp = _score_documentation(content)
    com_score, com_imp = _score_commercial(content, settings)
    sec_score, sec_imp = _score_security(analysis)
    comp_score, comp_imp = _score_completeness(content, media, settings)

    overall = (seo_score * 0.2 + doc_score * 0.2 + com_score * 0.3 + sec_score * 0.15 + comp_score * 0.15)

    all_improvements = seo_imp + doc_imp + com_imp + sec_imp + comp_imp

    return QualityScores(
        overall=round(overall, 1),
        seo=round(seo_score, 1),
        documentation=round(doc_score, 1),
        commercial=round(com_score, 1),
        security=round(sec_score, 1),
        completeness=round(comp_score, 1),
        improvements=all_improvements[:10],
    )

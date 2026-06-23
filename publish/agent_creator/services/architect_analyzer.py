"""Analyse métier rule-based pour l'AI Solution Architect (MVP)."""

import re
from dataclasses import dataclass


@dataclass
class ArchitectProposal:
    title: str
    description: str
    need: str


DOMAIN_RULES: list[dict] = [
    {
        "keywords": ["comptab", "cabinet", "expert-comptable", "facture", "écriture"],
        "processes": 12,
        "proposals": [
            ("Agent factures", "Traitement automatique des factures entrantes", "Je veux automatiser le traitement des factures clients et fournisseurs"),
            ("Agent rapprochement", "Rapprochement bancaire assisté", "Je veux automatiser le rapprochement bancaire"),
            ("Workflow validation", "Circuit de validation des écritures", "Je veux automatiser l'approbation des écritures comptables"),
            ("Assistant client", "Réponses aux demandes clients récurrentes", "Je veux un assistant pour répondre aux emails clients"),
        ],
    },
    {
        "keywords": ["rh", "recrut", "employé", "paie", "congé"],
        "processes": 9,
        "proposals": [
            ("Assistant RH", "Réponses aux questions employés", "Je veux un assistant RH pour les demandes internes"),
            ("Workflow recrutement", "Suivi candidats et entretiens", "Je veux suivre mes candidats et automatiser le recrutement"),
            ("Gestion congés", "Validation et suivi des absences", "Je veux automatiser la gestion des congés"),
        ],
    },
    {
        "keywords": ["crm", "prospect", "commercial", "vente", "client"],
        "processes": 10,
        "proposals": [
            ("Suivi prospects", "Relances et scoring automatiques", "Je veux suivre mes prospects"),
            ("Agent devis", "Génération et suivi des devis", "Je veux automatiser la création et le suivi des devis"),
            ("Assistant commercial", "Synthèse des interactions clients", "Je veux un assistant pour mes commerciaux"),
        ],
    },
    {
        "keywords": ["jurid", "contrat", "conformité", "rgpd"],
        "processes": 7,
        "proposals": [
            ("Analyse contrats", "Extraction des clauses clés", "Je veux analyser automatiquement mes contrats"),
            ("Veille conformité", "Alertes et checklists réglementaires", "Je veux une veille conformité automatisée"),
        ],
    },
    {
        "keywords": ["éducation", "formation", "école", "étudiant"],
        "processes": 8,
        "proposals": [
            ("Assistant pédagogique", "Réponses aux questions fréquentes", "Je veux un assistant pour les questions des apprenants"),
            ("Suivi inscriptions", "Automatisation des parcours d'inscription", "Je veux automatiser le suivi des inscriptions"),
        ],
    },
]


def _extract_employee_count(text: str) -> int | None:
    match = re.search(r"(\d+)\s*(employ|salari|person|collabor)", text, re.I)
    if match:
        return int(match.group(1))
    match = re.search(r"(\d+)", text)
    return int(match.group(1)) if match else None


def analyze_business(description: str) -> dict:
    text = description.strip().lower()
    employees = _extract_employee_count(text) or 10

    matched = DOMAIN_RULES[0]
    for rule in DOMAIN_RULES:
        if any(kw in text for kw in rule["keywords"]):
            matched = rule
            break

    processes = matched["processes"]
    if employees > 20:
        processes += 4
    elif employees < 5:
        processes = max(4, processes - 3)

    batch_hours = processes * (18 if employees > 15 else 12)
    monthly_cost = round(89 + employees * 9 + processes * 3, 0)
    hourly_value = 45
    annual_savings = batch_hours * hourly_value
    annual_cost = monthly_cost * 12
    roi = int(round((annual_savings - annual_cost) / annual_cost * 100)) if annual_cost else 0

    proposals = [
        ArchitectProposal(title=t, description=d, need=n)
        for t, d, n in matched["proposals"]
    ]

    return {
        "processes_count": processes,
        "hours_saved_per_year": batch_hours,
        "monthly_cost_eur": monthly_cost,
        "roi_percent": max(roi, 120),
        "proposals": [
            {"title": p.title, "description": p.description, "need": p.need}
            for p in proposals
        ],
        "summary": (
            f"Pour une organisation d'environ {employees} personnes, "
            f"j'ai identifié {processes} processus automatisables à fort impact."
        ),
    }

import json
import re

from agent_creator.models.blueprint import Blueprint, BlueprintComponent
from agent_creator.models.conversation import Conversation
from agent_creator.models.requirement import Requirement, SolutionType
from agent_creator.services.extractor import RequirementExtractor


class BlueprintGenerator:
    """Génère un blueprint d'architecture à partir des exigences."""

    def __init__(self, extractor: RequirementExtractor) -> None:
        self._extractor = extractor
        self._llm = extractor._llm

    async def generate(self, conversation: Conversation) -> Blueprint:
        requirements = await self._extractor.extract(conversation)
        clarifying = list(requirements.missing_information)

        if not self._llm.is_mock_mode:
            return await self._llm_generate(conversation.id, requirements, conversation.transcript)

        # --- Mode démo (sans LLM) ---
        if self._is_invoice_scenario(requirements):
            return self._invoice_blueprint(conversation.id, requirements, clarifying)

        solution_type, rationale, secondary = self._recommend_solution_type(requirements)
        components = self._generic_components(requirements, solution_type)
        data_flow = self._generic_data_flow(requirements, components)

        return Blueprint(
            conversation_id=conversation.id,
            title=requirements.summary or "Solution métier proposée",
            solution_type=solution_type,
            solution_type_rationale=rationale,
            secondary_types=secondary,
            requirements=requirements,
            components=components,
            data_flow=data_flow,
            clarifying_questions=clarifying[:5],
            next_steps=self._next_steps(requirements, solution_type),
            confidence=requirements.completeness_score,
        )

    async def _llm_generate(
        self,
        conversation_id: str,
        requirements: Requirement,
        transcript: str,
    ) -> Blueprint:
        """Génère un blueprint précis via LLM basé sur la vraie conversation."""
        schema_hint = json.dumps(
            {
                "title": "Titre court décrivant précisément la solution",
                "solution_type": "agent | workflow | hybrid | api | microservice",
                "solution_type_rationale": "Explication du choix en 1-2 phrases",
                "secondary_types": ["workflow", "agent"],
                "components": [
                    {
                        "name": "Nom du composant",
                        "type": "ai | integration | workflow | database | reporting | security",
                        "description": "Ce que fait ce composant précisément",
                        "technology_hint": "Technologies ou services recommandés",
                    }
                ],
                "data_flow": [
                    "1. Étape 1 du flux de données",
                    "2. Étape 2",
                ],
                "clarifying_questions": ["Question sur un point non précisé"],
                "next_steps": ["Action concrète à mener"],
            },
            ensure_ascii=False,
        )

        system_prompt = (
            "Tu es un architecte de solutions IA expert. "
            "Analyse la conversation ci-dessous et génère un blueprint d'architecture "
            "précis et adapté au cas métier spécifique décrit. "
            "Les composants doivent être concrets et directement liés aux systèmes "
            "mentionnés dans la conversation. "
            "NE génère PAS de composants génériques ou de templates. "
            "Chaque composant doit avoir un sens direct par rapport au besoin exprimé. "
            "Réponds uniquement en JSON valide selon ce schéma :\n" + schema_hint
        )

        user_prompt = (
            f"Conversation :\n{transcript}\n\n"
            f"Exigences extraites :\n"
            f"- Domaine : {requirements.domain}\n"
            f"- Objectifs : {'; '.join(requirements.objectives)}\n"
            f"- Sources de données : {'; '.join(requirements.data_sources)}\n"
            f"- Contraintes : {'; '.join(requirements.constraints)}\n"
            f"- Volumes : {'; '.join(requirements.volumes)}\n"
        )

        raw = await self._llm.structured_completion(
            system_prompt=system_prompt,
            user_prompt=user_prompt,
            schema_hint=schema_hint,
        )

        components = [
            BlueprintComponent(
                name=c.get("name", "Composant"),
                type=c.get("type", "workflow"),
                description=c.get("description", ""),
                technology_hint=c.get("technology_hint"),
            )
            for c in raw.get("components", [])
        ]

        secondary_raw = raw.get("secondary_types", [])
        secondary = []
        for st in secondary_raw:
            try:
                secondary.append(SolutionType(st))
            except ValueError:
                pass

        solution_type = SolutionType.HYBRID
        try:
            solution_type = SolutionType(raw.get("solution_type", "hybrid"))
        except ValueError:
            pass

        return Blueprint(
            conversation_id=conversation_id,
            title=raw.get("title", requirements.summary or "Solution proposée"),
            solution_type=solution_type,
            solution_type_rationale=raw.get("solution_type_rationale", ""),
            secondary_types=secondary,
            requirements=requirements,
            components=components,
            data_flow=raw.get("data_flow", []),
            clarifying_questions=raw.get("clarifying_questions", [])[:5],
            next_steps=raw.get("next_steps", []),
            confidence=requirements.completeness_score,
        )

    def _invoice_blueprint(
        self,
        conversation_id: str,
        requirements: Requirement,
        clarifying: list[str],
    ) -> Blueprint:
        return Blueprint(
            conversation_id=conversation_id,
            title="Automatisation du traitement des factures par email",
            solution_type=SolutionType.HYBRID,
            solution_type_rationale=(
                "Le besoin combine orchestration de workflow (réception, validation, écriture), "
                "composants IA (extraction documentaire) et intégrations API — un modèle hybride "
                "workflow + agent IA est recommandé."
            ),
            secondary_types=[SolutionType.WORKFLOW, SolutionType.AGENT],
            requirements=requirements,
            components=[
                BlueprintComponent(
                    name="Connecteur email",
                    type="integration",
                    description="Surveillance de la boîte de réception et récupération des pièces jointes",
                    technology_hint="Microsoft Graph API / IMAP / Gmail API",
                ),
                BlueprintComponent(
                    name="Moteur d'extraction IA",
                    type="ai",
                    description="OCR et extraction structurée des champs facture (fournisseur, montant, TVA, date)",
                    technology_hint="Modèle vision + LLM structuré",
                ),
                BlueprintComponent(
                    name="Workflow d'orchestration",
                    type="workflow",
                    description="Enchaînement réception → extraction → validation → écriture → notification",
                    technology_hint="Temporal / n8n / Azure Logic Apps",
                ),
                BlueprintComponent(
                    name="Connecteur comptable",
                    type="integration",
                    description="Création des écritures comptables dans le système cible",
                    technology_hint="API Sage / QuickBooks / export CSV",
                ),
                BlueprintComponent(
                    name="Stockage métier",
                    type="database",
                    description="Persistance des factures traitées, statuts et audit trail",
                    technology_hint="PostgreSQL",
                ),
                BlueprintComponent(
                    name="Générateur de rapport quotidien",
                    type="reporting",
                    description="Agrégation des traitements du jour et envoi du rapport",
                    technology_hint="PDF + planificateur cron",
                ),
            ],
            data_flow=[
                "1. Réception d'un email avec facture en pièce jointe",
                "2. Déclenchement du workflow et stockage du document brut",
                "3. Extraction IA des champs structurés de la facture",
                "4. Validation optionnelle (règles métier ou humain)",
                "5. Écriture dans le système comptable via connecteur",
                "6. Mise à jour du statut et de l'audit trail en base",
                "7. Génération et envoi du rapport quotidien consolidé",
            ],
            clarifying_questions=clarifying[:5] or [
                "Quel système comptable utilisez-vous ?",
                "Quel est le volume moyen de factures par jour ?",
                "Le rapport quotidien doit-il être validé avant envoi ?",
            ],
            next_steps=[
                "Valider le système comptable et le mode d'intégration",
                "Définir le schéma de données facture cible",
                "Prototyper l'extraction sur un échantillon de factures réelles",
                "Configurer le workflow et les alertes d'erreur",
                "Planifier la phase de recette avec les équipes comptables",
            ],
            confidence=requirements.completeness_score,
        )

    @staticmethod
    def _is_invoice_scenario(requirements: Requirement) -> bool:
        text = " ".join(requirements.objectives + [requirements.domain or ""]).lower()
        return "facture" in text or (requirements.domain and "comptab" in requirements.domain.lower())

    @staticmethod
    def _recommend_solution_type(
        requirements: Requirement,
    ) -> tuple[SolutionType, str, list[SolutionType]]:
        text = " ".join(requirements.objectives + requirements.data_sources).lower()

        if any(w in text for w in ("agent", "ia", "intelligence", "llm", "chat")):
            return (
                SolutionType.HYBRID,
                "Le besoin implique de l'IA et de l'orchestration — approche hybride recommandée.",
                [SolutionType.AGENT, SolutionType.WORKFLOW],
            )
        if any(w in text for w in ("api", "endpoint", "rest", "webhook")):
            return (
                SolutionType.API,
                "Exposition de capacités via API adaptée aux intégrations système.",
                [],
            )
        if any(w in text for w in ("microservice", "service", "scalab")):
            return (
                SolutionType.MICROSERVICE,
                "Découpage en microservice pour scalabilité et déploiement indépendant.",
                [],
            )
        if any(w in text for w in ("workflow", "automat", "processus", "étape")):
            return (
                SolutionType.WORKFLOW,
                "Processus séquentiel avec étapes automatisées — workflow recommandé.",
                [],
            )

        return (
            SolutionType.WORKFLOW,
            "Blueprint initial basé sur un workflow d'orchestration (type par défaut MVP).",
            [],
        )

    def _generic_components(
        self,
        requirements: Requirement,
        solution_type: SolutionType,
    ) -> list[BlueprintComponent]:
        components = [
            BlueprintComponent(
                name="Orchestrateur",
                type="workflow",
                description="Coordination des étapes du processus métier",
                technology_hint="n8n / Temporal",
            ),
        ]

        if requirements.data_sources:
            components.append(
                BlueprintComponent(
                    name="Couche d'intégration",
                    type="integration",
                    description=f"Connexion aux sources : {', '.join(requirements.data_sources[:3])}",
                )
            )

        if solution_type in (SolutionType.AGENT, SolutionType.HYBRID):
            components.append(
                BlueprintComponent(
                    name="Agent IA",
                    type="ai",
                    description="Raisonnement et traitement intelligent des données",
                    technology_hint="LLM + outils métier",
                )
            )

        components.append(
            BlueprintComponent(
                name="Persistance",
                type="database",
                description="Stockage des données et journaux d'audit",
                technology_hint="PostgreSQL",
            )
        )

        return components

    @staticmethod
    def _generic_data_flow(
        requirements: Requirement,
        components: list[BlueprintComponent],
    ) -> list[str]:
        flow = ["1. Déclenchement du processus (événement ou planification)"]
        for i, comp in enumerate(components[1:], start=2):
            flow.append(f"{i}. Traitement via {comp.name}")
        flow.append(f"{len(components) + 1}. Notification / restitution du résultat")
        return flow

    @staticmethod
    def _next_steps(requirements: Requirement, solution_type: SolutionType) -> list[str]:
        steps = [
            f"Valider le type de solution recommandé : {solution_type.value}",
            "Compléter les informations manquantes identifiées",
            "Définir les critères d'acceptation et les SLA",
        ]
        if requirements.missing_information:
            steps.insert(1, "Organiser un atelier de cadrage avec les parties prenantes")
        return steps

from agent_creator.models.conversation import Conversation
from agent_creator.models.requirement import Requirement
from agent_creator.services.llm import LLMService


class RequirementExtractor:
    """Extrait les exigences structurées depuis le dialogue."""

    def __init__(self, llm: LLMService) -> None:
        self._llm = llm

    async def extract(self, conversation: Conversation) -> Requirement:
        transcript = conversation.transcript
        if self._llm.is_mock_mode:
            return self._rule_based_extract(transcript)

        schema_hint = """
        {
          "objectives": ["string"],
          "constraints": ["string"],
          "data_sources": ["string"],
          "volumes": ["string"],
          "risks": ["string"],
          "domain": "string or null",
          "summary": "string",
          "completeness_score": 0.0-1.0,
          "missing_information": ["string"]
        }
        """
        data = await self._llm.structured_completion(
            system_prompt=(
                "Analyse la conversation et extrais les exigences métier en français. "
                "Sois précis et factuel."
            ),
            user_prompt=transcript,
            schema_hint=schema_hint,
        )
        return Requirement.model_validate(data)

    def _rule_based_extract(self, transcript: str) -> Requirement:
        text = transcript.lower()

        if self._is_invoice_scenario(text):
            return Requirement(
                objectives=[
                    "Automatiser la réception et le traitement des factures par email",
                    "Extraire les données clés des factures (montant, fournisseur, date, TVA)",
                    "Enregistrer les écritures dans le système comptable",
                    "Produire et distribuer un rapport quotidien de synthèse",
                ],
                constraints=[
                    "Fiabilité de l'extraction documentaire (factures PDF et pièces jointes)",
                    "Traçabilité des opérations comptables",
                    "Respect des délais de clôture comptable",
                ],
                data_sources=[
                    "Boîte email (IMAP/Exchange/Gmail)",
                    "Pièces jointes factures (PDF, images)",
                    "Système comptable (API ou export)",
                ],
                volumes=[
                    "Traitement continu à chaque réception de facture",
                    "Rapport quotidien agrégé",
                ],
                risks=[
                    "Erreurs d'extraction OCR/IA sur factures non standard",
                    "Doublons de factures",
                    "Données sensibles dans les emails",
                    "Indisponibilité du connecteur comptable",
                ],
                domain="Comptabilité / automatisation documentaire",
                summary=(
                    "Workflow automatisé : réception email → extraction IA des factures → "
                    "intégration comptable → rapport quotidien."
                ),
                completeness_score=0.72,
                missing_information=[
                    "Système comptable cible (nom et mode d'intégration)",
                    "Volume exact de factures (jour/mois)",
                    "Format et destinataires du rapport quotidien",
                    "Besoin de validation humaine avant écriture comptable",
                ],
            )

        return self._generic_extract(text)

    @staticmethod
    def _is_invoice_scenario(text: str) -> bool:
        invoice_signals = ("facture", "comptab", "email")
        return sum(1 for s in invoice_signals if s in text) >= 2

    def _generic_extract(self, text: str) -> Requirement:
        objectives: list[str] = []
        constraints: list[str] = []
        data_sources: list[str] = []
        volumes: list[str] = []
        risks: list[str] = []
        missing: list[str] = []

        if any(w in text for w in ("automat", "workflow", "processus")):
            objectives.append("Automatiser un processus métier")
        if any(w in text for w in ("rapport", "dashboard", "tableau")):
            objectives.append("Produire des rapports ou tableaux de bord")
        if any(w in text for w in ("extraire", "extraction", "analyser")):
            objectives.append("Extraire et structurer des données")

        if "api" in text:
            data_sources.append("API externe")
        if "email" in text:
            data_sources.append("Email")
        if any(w in text for w in ("base de données", "postgresql", "sql")):
            data_sources.append("Base de données")

        if any(w in text for w in ("quotidien", "journalier", "chaque jour")):
            volumes.append("Exécution quotidienne")
        if any(w in text for w in ("temps réel", "real-time", "instantan")):
            volumes.append("Traitement en temps réel")

        if any(w in text for w in ("rgpd", "conform", "sécurit")):
            constraints.append("Exigences de conformité et sécurité des données")

        if not objectives:
            objectives.append("Répondre au besoin métier décrit par l'utilisateur")
            missing.append("Objectifs métier précis")

        if not data_sources:
            missing.append("Sources de données identifiées")

        if not volumes:
            missing.append("Volumes et fréquences d'exécution")

        risks.append("Informations incomplètes — blueprint provisoire")

        score = max(0.3, 0.8 - 0.15 * len(missing))

        return Requirement(
            objectives=objectives,
            constraints=constraints or ["À préciser avec l'utilisateur"],
            data_sources=data_sources,
            volumes=volumes,
            risks=risks,
            domain=self._guess_domain(text),
            summary=self._build_summary(objectives),
            completeness_score=round(score, 2),
            missing_information=missing,
        )

    @staticmethod
    def _guess_domain(text: str) -> str | None:
        domains = {
            "comptab": "Comptabilité",
            "rh": "Ressources humaines",
            "vente": "Commercial",
            "client": "Relation client",
            "logist": "Logistique",
        }
        for key, label in domains.items():
            if key in text:
                return label
        return None

    @staticmethod
    def _build_summary(objectives: list[str]) -> str:
        if not objectives:
            return "Besoin métier à structurer"
        return " ; ".join(objectives[:3])

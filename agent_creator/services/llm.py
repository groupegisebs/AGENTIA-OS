import json
import re
from typing import Any

import httpx

from agent_creator.config import Settings


class LLMService:
    """Client OpenAI-compatible avec repli rule-based hors-ligne."""

    SYSTEM_PROMPT = (
        "Tu es l'Architecte IA d'Agentia Factory. Tu mènes un entretien de découverte métier "
        "rigoureux pour concevoir un agent IA réellement fonctionnel et utile.\n\n"

        "OBJECTIF : Collecter toutes les informations nécessaires pour configurer un agent IA "
        "capable d'exécuter des tâches précises et de délivrer un résultat mesurable.\n\n"

        "PROCESSUS EN 4 PHASES :\n"
        "Phase 1 – Contexte : secteur d'activité, rôle de l'utilisateur, problème actuel en détail\n"
        "Phase 2 – Processus cible : décrire chaque étape que l'agent devra effectuer\n"
        "Phase 3 – Données & systèmes : données en entrée/sortie, outils et systèmes à connecter\n"
        "Phase 4 – Contraintes & succès : critères de réussite, volumes, limites, validation humaine\n\n"

        "RÈGLES ABSOLUES :\n"
        "- Pose exactement 2 à 3 questions précises par message — jamais moins, jamais plus\n"
        "- Ne passe PAS à la phase suivante avant d'avoir des réponses concrètes\n"
        "- Si la réponse est vague ('automatiser les emails'), reformule et demande des exemples précis\n"
        "- Reformule toujours ta compréhension avant de poser la question suivante\n"
        "- N'annonce JAMAIS que tu vas générer un blueprint avant d'avoir toutes les phases\n"
        "- Adapte tes questions au secteur : comptabilité → logiciels comptables, "
        "RH → SIRH et processus de paie, commercial → CRM et pipeline\n"
        "- Réponds UNIQUEMENT en français, ton professionnel et direct\n\n"

        "INFORMATIONS OBLIGATOIRES AVANT LE BLUEPRINT :\n"
        "✓ Action concrète de l'agent (pas juste 'automatiser', mais 'lire les emails et extraire X')\n"
        "✓ Documents ou données traités (type, format, source)\n"
        "✓ Systèmes impliqués (noms exacts : Gmail/Outlook/Sage/Salesforce/etc.)\n"
        "✓ Déclencheur de l'agent (nouvel email, planification, bouton manuel, webhook)\n"
        "✓ Résultat produit (rapport PDF, entrée CRM, notification Slack, mise à jour base)\n"
        "✓ Contraintes critiques (validation manuelle requise, volume, RGPD, délais)\n\n"

        "Ne mentionne la facturation QUE si l'utilisateur parle de déploiement ou mise en production."
    )

    def __init__(self, settings: Settings) -> None:
        self._settings = settings

    @property
    def is_mock_mode(self) -> bool:
        return not self._settings.llm_enabled

    @property
    def mode_label(self) -> str:
        return self._settings.active_llm_provider

    async def chat(self, messages: list[dict[str, str]]) -> str:
        if self.is_mock_mode:
            return self._mock_chat(messages)
        if self._settings.active_llm_provider == "gemini":
            return await self._gemini_chat(messages)
        return await self._openai_chat(messages)

    async def structured_completion(
        self,
        system_prompt: str,
        user_prompt: str,
        schema_hint: str,
    ) -> dict[str, Any]:
        if self.is_mock_mode:
            return {}
        payload_messages = [
            {"role": "system", "content": f"{system_prompt}\n\nRéponds uniquement en JSON valide.\n{schema_hint}"},
            {"role": "user", "content": user_prompt},
        ]
        if self._settings.active_llm_provider == "gemini":
            raw = await self._gemini_chat(payload_messages, json_mode=True)
        else:
            raw = await self._openai_chat(payload_messages, json_mode=True)
        try:
            return json.loads(raw)
        except json.JSONDecodeError:
            match = re.search(r"\{.*\}", raw, re.DOTALL)
            if match:
                return json.loads(match.group())
            raise

    async def _gemini_chat(self, messages: list[dict[str, str]], json_mode: bool = False) -> str:
        """Appel Google Gemini — clé via GEMINI_API_KEY (secret GHA, jamais loggée)."""
        system_parts: list[str] = []
        contents: list[dict[str, Any]] = []
        for msg in messages:
            if msg["role"] == "system":
                system_parts.append(msg["content"])
                continue
            role = "user" if msg["role"] == "user" else "model"
            contents.append({"role": role, "parts": [{"text": msg["content"]}]})

        body: dict[str, Any] = {"contents": contents}
        if system_parts:
            body["systemInstruction"] = {"parts": [{"text": "\n".join(system_parts)}]}
        if json_mode:
            body["generationConfig"] = {"responseMimeType": "application/json"}

        model = self._settings.gemini_model
        url = f"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent"
        params = {"key": self._settings.gemini_api_key}

        async with httpx.AsyncClient(timeout=60.0) as client:
            response = await client.post(url, params=params, json=body)
            response.raise_for_status()
            data = response.json()
            candidates = data.get("candidates") or []
            if not candidates:
                raise RuntimeError("Réponse Gemini vide")
            parts = candidates[0].get("content", {}).get("parts") or []
            return "".join(p.get("text", "") for p in parts)

    async def _openai_chat(self, messages: list[dict[str, str]], json_mode: bool = False) -> str:
        headers = {
            "Authorization": f"Bearer {self._settings.openai_api_key}",
            "Content-Type": "application/json",
        }
        body: dict[str, Any] = {
            "model": self._settings.openai_model,
            "messages": messages,
            "temperature": 0.4,
        }
        if json_mode:
            body["response_format"] = {"type": "json_object"}

        async with httpx.AsyncClient(timeout=60.0) as client:
            response = await client.post(
                f"{self._settings.openai_base_url.rstrip('/')}/chat/completions",
                headers=headers,
                json=body,
            )
            response.raise_for_status()
            data = response.json()
            return data["choices"][0]["message"]["content"]

    def _mock_chat(self, messages: list[dict[str, str]]) -> str:
        """
        Mode démonstration sans LLM — simule une découverte métier structurée.
        Pour des résultats réels, configurez GEMINI_API_KEY ou OPENAI_API_KEY.
        """
        user_msgs = [m for m in messages if m["role"] == "user"]
        user_turns = len(user_msgs)
        last_user = user_msgs[-1]["content"] if user_msgs else ""
        last_lower = last_user.lower()

        full_context = " ".join(m["content"].lower() for m in user_msgs)

        domain = self._detect_domain(full_context)

        if user_turns == 1:
            return self._phase1_response(last_lower, domain)
        if user_turns == 2:
            return self._phase2_response(last_lower, domain, full_context)
        if user_turns == 3:
            return self._phase3_response(last_lower, domain, full_context)
        if user_turns == 4:
            return self._phase4_response(domain)

        return (
            "Merci, j'ai toutes les informations nécessaires pour construire votre agent.\n\n"
            "**Récapitulatif** : J'ai bien noté votre processus, les systèmes impliqués, "
            "vos contraintes et le résultat attendu.\n\n"
            "Cliquez sur **Générer le blueprint** pour voir l'architecture proposée, "
            "puis déployez votre agent en un clic."
        )

    @staticmethod
    def _detect_domain(text: str) -> str:
        if any(w in text for w in ("facture", "comptab", "sage", "quickbooks", "tva", "écriture")):
            return "comptabilité"
        if any(w in text for w in ("candidat", "recrutement", "cv", "embauche", "rh", "paie")):
            return "rh"
        if any(w in text for w in ("prospect", "crm", "vente", "devis", "commercial", "salesforce")):
            return "commercial"
        if any(w in text for w in ("client", "support", "ticket", "réclamation", "helpdesk")):
            return "support"
        if any(w in text for w in ("stock", "commande", "logistique", "livraison", "inventaire")):
            return "logistique"
        if any(w in text for w in ("contrat", "juridique", "clause", "conformité", "rgpd")):
            return "juridique"
        if any(w in text for w in ("rapport", "dashboard", "kpi", "analyse", "données")):
            return "reporting"
        return "général"

    @staticmethod
    def _phase1_response(last_lower: str, domain: str) -> str:
        domain_questions = {
            "comptabilité": (
                "Pour bien comprendre votre besoin, j'ai quelques questions :\n\n"
                "1. **Le processus exact** : Quelles étapes faites-vous manuellement aujourd'hui "
                "que vous souhaitez automatiser ? (ex : saisir des factures dans Sage, rapprocher "
                "des relevés bancaires, générer des rapports mensuels)\n"
                "2. **Les systèmes impliqués** : Quel logiciel comptable utilisez-vous "
                "(Sage, Cegid, QuickBooks, EBP, autre) ? Les données arrivent par email, "
                "via un dossier partagé, ou autrement ?\n"
                "3. **Le volume** : Combien de documents ou d'opérations traitez-vous par jour/semaine ?"
            ),
            "rh": (
                "Pour construire un agent RH efficace, j'ai besoin de comprendre :\n\n"
                "1. **Le processus exact** : Quelle tâche RH souhaitez-vous automatiser ? "
                "(tri de CV, onboarding, suivi des congés, génération de contrats, autre)\n"
                "2. **Les outils actuels** : Utilisez-vous un SIRH (BambooHR, Workday, Lucca, autre) ? "
                "Les données sont-elles dans des emails, des formulaires, des fichiers Excel ?\n"
                "3. **Le déclencheur** : Qu'est-ce qui déclenche le processus ? "
                "(nouvelle candidature, demande d'un manager, planification hebdomadaire)"
            ),
            "commercial": (
                "Pour concevoir votre agent commercial, j'ai besoin de précisions :\n\n"
                "1. **Le processus exact** : Quelle tâche commerciale souhaitez-vous automatiser ? "
                "(qualification de leads, relances, génération de devis, mise à jour CRM)\n"
                "2. **Le CRM utilisé** : Salesforce, HubSpot, Pipedrive, autre ? "
                "Les prospects arrivent d'où (formulaire web, LinkedIn, email) ?\n"
                "3. **Le résultat attendu** : Que doit produire l'agent ? "
                "(fiche prospect complétée, email de relance envoyé, devis généré, rapport)"
            ),
        }
        return domain_questions.get(
            domain,
            (
                "Pour construire un agent vraiment utile, j'ai besoin de comprendre votre besoin en détail.\n\n"
                "1. **Le processus actuel** : Décrivez-moi étape par étape ce que vous faites "
                "manuellement aujourd'hui et que vous voulez automatiser.\n"
                "2. **Les données impliquées** : Quels types de données ou documents traite ce processus ? "
                "D'où viennent-ils et où vont-ils ?\n"
                "3. **Le résultat attendu** : Qu'est-ce que l'agent doit produire concrètement ? "
                "(rapport, notification, mise à jour d'un système, action automatique)"
            ),
        )

    @staticmethod
    def _phase2_response(last_lower: str, domain: str, full_context: str) -> str:
        return (
            "Merci, je commence à avoir une image claire. Maintenant les détails techniques :\n\n"
            "1. **Les systèmes sources** : Nommez précisément les logiciels ou services d'où "
            "l'agent doit lire les données (ex : Gmail spécifique, dossier SharePoint, "
            "base de données MySQL, API d'un logiciel métier).\n"
            "2. **Les systèmes cibles** : Où doit-il écrire ou envoyer les résultats ? "
            "(logiciel comptable, CRM, email, Slack, fichier Excel, base de données)\n"
            "3. **Le déclencheur** : Qu'est-ce qui doit activer l'agent ? "
            "(arrivée d'un email précis, à une heure fixe chaque jour, clic d'un utilisateur, "
            "webhook d'un autre système)"
        )

    @staticmethod
    def _phase3_response(last_lower: str, domain: str, full_context: str) -> str:
        return (
            "Excellent. Dernières questions avant de construire votre agent :\n\n"
            "1. **Validation humaine** : L'agent doit-il agir de façon autonome, ou faut-il "
            "une approbation humaine avant certaines actions ? Si oui, pour quelles étapes ?\n"
            "2. **Gestion des erreurs** : Que doit-il faire en cas de problème ? "
            "(alerter par email, mettre en attente, ignorer et passer au suivant)\n"
            "3. **Contraintes particulières** : Y a-t-il des données sensibles (RGPD), "
            "des délais à respecter, ou des cas particuliers à gérer ?"
        )

    @staticmethod
    def _phase4_response(domain: str) -> str:
        return (
            "J'ai maintenant tous les éléments pour construire votre agent.\n\n"
            "**Récapitulatif de ce que j'ai compris** :\n"
            "- Processus, systèmes sources/cibles et déclencheur identifiés\n"
            "- Contraintes et niveaux de validation notés\n"
            "- Architecture adaptée à votre domaine : **" + domain + "**\n\n"
            "Cliquez sur **Générer le blueprint** pour voir l'architecture proposée et "
            "les composants techniques. La génération est gratuite."
        )

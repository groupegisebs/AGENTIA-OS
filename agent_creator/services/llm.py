import json
import re
from typing import Any

import httpx

from agent_creator.config import Settings


class LLMService:
    """Client OpenAI-compatible avec repli rule-based hors-ligne."""

    SYSTEM_PROMPT = (
        "Tu es l'Agent Creator d'Agentia Factory, plateforme SaaS avec abonnement. "
        "Tu dialogues en français avec l'utilisateur pour comprendre son besoin métier. "
        "Pose des questions de clarification ciblées, reformule les objectifs et contraintes, "
        "et indique quand tu as assez d'informations pour produire un blueprint de solution. "
        "La génération du blueprint est gratuite ; le déploiement de l'agent est facturé "
        "à chaque fois selon le plan d'abonnement (Gratuit, Professionnel, Business, Entreprise). "
        "Mentionne ce modèle lorsque l'utilisateur parle de mise en production ou de déploiement."
    )

    def __init__(self, settings: Settings) -> None:
        self._settings = settings

    @property
    def is_mock_mode(self) -> bool:
        return not self._settings.llm_enabled

    async def chat(self, messages: list[dict[str, str]]) -> str:
        if self.is_mock_mode:
            return self._mock_chat(messages)
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
        raw = await self._openai_chat(payload_messages, json_mode=True)
        try:
            return json.loads(raw)
        except json.JSONDecodeError:
            match = re.search(r"\{.*\}", raw, re.DOTALL)
            if match:
                return json.loads(match.group())
            raise

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
        last_user = ""
        for msg in reversed(messages):
            if msg["role"] == "user":
                last_user = msg["content"].lower()
                break

        user_turns = sum(1 for m in messages if m["role"] == "user")

        if self._is_invoice_flow(last_user):
            if user_turns == 1:
                return (
                    "Merci pour cette description. Je comprends un besoin d'automatisation de traitement "
                    "des factures reçues par email.\n\n"
                    "Pour affiner le blueprint, pouvez-vous préciser :\n"
                    "1. Quel système comptable utilisez-vous (Sage, QuickBooks, autre) ?\n"
                    "2. Quel volume de factures par jour ou par mois ?\n"
                    "3. Le rapport quotidien doit-il être envoyé par email, PDF, ou tableau de bord ?\n"
                    "4. Y a-t-il des exigences de conformité ou de validation humaine ?"
                )
            return (
                "Parfait, j'ai maintenant une vision claire de votre besoin. "
                "Je peux générer un blueprint de solution combinant un workflow d'orchestration "
                "et des composants d'extraction IA.\n\n"
                "Utilisez GET /conversations/{id}/blueprint pour obtenir l'architecture proposée "
                "(gratuit). Le déploiement de l'agent via POST /conversations/{id}/deploy "
                "est facturé selon votre plan d'abonnement."
            )

        if user_turns == 1:
            return (
                "Merci pour votre description. Pour structurer votre besoin, pouvez-vous préciser :\n"
                "1. Quel est l'objectif principal à atteindre ?\n"
                "2. Quelles sont vos sources de données ?\n"
                "3. Y a-t-il des contraintes (budget, délais, conformité) ?\n"
                "4. Quel volume ou quelle fréquence d'exécution envisagez-vous ?"
            )

        return (
            "J'ai noté vos précisions. Je dispose de suffisamment d'éléments pour proposer "
            "un blueprint initial (gratuit). Consultez GET /conversations/{id}/blueprint. "
            "Le déploiement en production est facturé à chaque agent déployé."
        )

    @staticmethod
    def _is_invoice_flow(text: str) -> bool:
        keywords = ("facture", "email", "comptab", "rapport")
        return sum(1 for kw in keywords if kw in text) >= 2

"""Service d'envoi d'emails via GiseMailSender (SecureMail Gateway)."""
from __future__ import annotations

import httpx

from agent_creator.config import Settings


class EmailService:
    """Envoie des emails transactionnels via GiseMailSender."""

    TEMPLATE_TRANSACTIONAL = "TRANSACTIONAL"
    TEMPLATE_RESET_PASSWORD = "RESET_PASSWORD"

    def __init__(self, settings: Settings) -> None:
        self._settings = settings

    @property
    def is_configured(self) -> bool:
        return self._settings.email_configured

    def _headers(self) -> dict[str, str]:
        return {
            "Authorization": f"Bearer {self._settings.gisemailsender_api_key}",
            "Content-Type": "application/json",
        }

    async def send_password_reset(self, to_email: str, full_name: str, reset_link: str) -> bool:
        """Envoie le lien de reset password. Essaie RESET_PASSWORD, fallback sur TRANSACTIONAL."""
        sent = await self._send(
            to=to_email,
            template_code=self.TEMPLATE_RESET_PASSWORD,
            body_data={
                "FullName": full_name,
                "FirstName": full_name.split()[0] if full_name else to_email.split("@")[0],
                "ResetLink": reset_link,
                "AppName": self._settings.gisemailsender_from_name,
            },
            subject_data={"Subject": f"Réinitialisation de votre mot de passe — {self._settings.gisemailsender_from_name}"},
        )
        if sent:
            return True

        html_body = (
            f"<p>Bonjour {full_name or 'utilisateur'},</p>"
            f"<p>Vous avez demandé la réinitialisation de votre mot de passe sur "
            f"<strong>{self._settings.gisemailsender_from_name}</strong>.</p>"
            f'<p><a href="{reset_link}" style="background:#7c3aed;color:#fff;padding:12px 24px;'
            f'border-radius:6px;text-decoration:none;display:inline-block;">Réinitialiser mon mot de passe</a></p>'
            f"<p>Ce lien expire dans 1 heure. Si vous n'êtes pas à l'origine de cette demande, "
            f"ignorez cet email.</p>"
        )
        return await self._send(
            to=to_email,
            template_code=self.TEMPLATE_TRANSACTIONAL,
            body_data={
                "Subject": f"Réinitialisation de votre mot de passe — {self._settings.gisemailsender_from_name}",
                "HtmlBody": html_body,
            },
            subject_data={"Subject": f"Réinitialisation de votre mot de passe — {self._settings.gisemailsender_from_name}"},
        )

    async def _send(
        self,
        to: str,
        template_code: str,
        body_data: dict[str, str],
        subject_data: dict[str, str] | None = None,
    ) -> bool:
        if not self.is_configured:
            return False

        base = self._settings.gisemailsender_url.rstrip("/")
        payload = {
            "clientCode": self._settings.gisemailsender_client_code,
            "templateCode": template_code,
            "to": [to],
            "bodyData": body_data,
        }
        if subject_data:
            payload["subjectData"] = subject_data

        try:
            async with httpx.AsyncClient(timeout=15.0) as client:
                response = await client.post(
                    f"{base}/api/mail/send",
                    headers=self._headers(),
                    json=payload,
                )
            return response.status_code in (200, 201, 202)
        except Exception:
            return False

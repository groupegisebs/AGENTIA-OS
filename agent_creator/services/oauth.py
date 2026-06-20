"""OAuth2 — Google, Facebook, GitHub, Microsoft."""

from __future__ import annotations

import secrets
from dataclasses import dataclass
from datetime import datetime, timedelta, timezone
from typing import Any
from urllib.parse import urlencode

import httpx
from jose import JWTError, jwt

from agent_creator.config import Settings

PROVIDERS = ("google", "facebook", "github", "microsoft")


class OAuthError(Exception):
    pass


@dataclass(frozen=True)
class OAuthProfile:
    provider: str
    provider_user_id: str
    email: str
    full_name: str


@dataclass(frozen=True)
class _ProviderConfig:
    name: str
    authorize_url: str
    token_url: str
    scopes: list[str]
    client_id: str
    client_secret: str
    extra_authorize_params: dict[str, str]


def _provider_configs(settings: Settings) -> dict[str, _ProviderConfig]:
    tenant = settings.oauth_microsoft_tenant_id or "common"
    return {
        "google": _ProviderConfig(
            name="google",
            authorize_url="https://accounts.google.com/o/oauth2/v2/auth",
            token_url="https://oauth2.googleapis.com/token",
            scopes=["openid", "email", "profile"],
            client_id=settings.oauth_google_client_id,
            client_secret=settings.oauth_google_client_secret,
            extra_authorize_params={"access_type": "online", "prompt": "select_account"},
        ),
        "facebook": _ProviderConfig(
            name="facebook",
            authorize_url="https://www.facebook.com/v21.0/dialog/oauth",
            token_url="https://graph.facebook.com/v21.0/oauth/access_token",
            scopes=["email", "public_profile"],
            client_id=settings.oauth_facebook_app_id,
            client_secret=settings.oauth_facebook_app_secret,
            extra_authorize_params={},
        ),
        "github": _ProviderConfig(
            name="github",
            authorize_url="https://github.com/login/oauth/authorize",
            token_url="https://github.com/login/oauth/access_token",
            scopes=["read:user", "user:email"],
            client_id=settings.oauth_github_client_id,
            client_secret=settings.oauth_github_client_secret,
            extra_authorize_params={},
        ),
        "microsoft": _ProviderConfig(
            name="microsoft",
            authorize_url=f"https://login.microsoftonline.com/{tenant}/oauth2/v2.0/authorize",
            token_url=f"https://login.microsoftonline.com/{tenant}/oauth2/v2.0/token",
            scopes=["openid", "email", "profile", "User.Read"],
            client_id=settings.oauth_microsoft_client_id,
            client_secret=settings.oauth_microsoft_client_secret,
            extra_authorize_params={},
        ),
    }


def list_enabled_providers(settings: Settings) -> list[str]:
    configs = _provider_configs(settings)
    enabled: list[str] = []
    for name in PROVIDERS:
        cfg = configs[name]
        if cfg.client_id.strip() and cfg.client_secret.strip():
            enabled.append(name)
    return enabled


def is_provider_enabled(settings: Settings, provider: str) -> bool:
    return provider in list_enabled_providers(settings)


def oauth_callback_url(settings: Settings, provider: str) -> str:
    base = settings.oauth_redirect_base_url.rstrip("/")
    return f"{base}/auth/oauth/{provider}/callback"


class OAuthService:
    def __init__(self, settings: Settings) -> None:
        self._settings = settings

    def create_state_token(self, provider: str) -> str:
        payload = {
            "provider": provider,
            "nonce": secrets.token_urlsafe(16),
            "exp": datetime.now(timezone.utc) + timedelta(minutes=10),
        }
        return jwt.encode(payload, self._settings.jwt_secret, algorithm=self._settings.jwt_algorithm)

    def verify_state_token(self, provider: str, state: str) -> None:
        try:
            payload = jwt.decode(
                state,
                self._settings.jwt_secret,
                algorithms=[self._settings.jwt_algorithm],
            )
        except JWTError as exc:
            raise OAuthError("État OAuth invalide ou expiré") from exc
        if payload.get("provider") != provider:
            raise OAuthError("État OAuth invalide")

    def build_authorize_url(self, provider: str) -> str:
        if provider not in PROVIDERS:
            raise OAuthError("Fournisseur OAuth inconnu")
        if not is_provider_enabled(self._settings, provider):
            raise OAuthError(f"Connexion {provider} non configurée")

        cfg = _provider_configs(self._settings)[provider]
        state = self.create_state_token(provider)
        redirect_uri = oauth_callback_url(self._settings, provider)
        params: dict[str, str] = {
            "client_id": cfg.client_id,
            "redirect_uri": redirect_uri,
            "response_type": "code",
            "scope": " ".join(cfg.scopes),
            "state": state,
            **cfg.extra_authorize_params,
        }
        return f"{cfg.authorize_url}?{urlencode(params)}"

    async def complete_login(self, provider: str, code: str, state: str) -> OAuthProfile:
        self.verify_state_token(provider, state)
        if not is_provider_enabled(self._settings, provider):
            raise OAuthError(f"Connexion {provider} non configurée")

        cfg = _provider_configs(self._settings)[provider]
        redirect_uri = oauth_callback_url(self._settings, provider)
        token_data = await self._exchange_code(cfg, code, redirect_uri)
        access_token = token_data.get("access_token")
        if not access_token:
            raise OAuthError("Jeton d'accès OAuth manquant")

        if provider == "google":
            return await self._profile_google(access_token)
        if provider == "facebook":
            return await self._profile_facebook(access_token)
        if provider == "github":
            return await self._profile_github(access_token)
        if provider == "microsoft":
            return await self._profile_microsoft(access_token)
        raise OAuthError("Fournisseur OAuth inconnu")

    async def _exchange_code(self, cfg: _ProviderConfig, code: str, redirect_uri: str) -> dict[str, Any]:
        data = {
            "client_id": cfg.client_id,
            "client_secret": cfg.client_secret,
            "code": code,
            "redirect_uri": redirect_uri,
            "grant_type": "authorization_code",
        }
        headers: dict[str, str] = {}
        if cfg.name == "github":
            headers["Accept"] = "application/json"

        async with httpx.AsyncClient(timeout=30.0) as client:
            response = await client.post(cfg.token_url, data=data, headers=headers)
            if response.status_code >= 400:
                raise OAuthError("Échec de l'échange du code OAuth")
            return response.json()

    async def _profile_google(self, access_token: str) -> OAuthProfile:
        async with httpx.AsyncClient(timeout=30.0) as client:
            response = await client.get(
                "https://www.googleapis.com/oauth2/v3/userinfo",
                headers={"Authorization": f"Bearer {access_token}"},
            )
            response.raise_for_status()
            data = response.json()
        email = (data.get("email") or "").lower()
        if not email:
            raise OAuthError("Google n'a pas fourni d'adresse email")
        return OAuthProfile(
            provider="google",
            provider_user_id=str(data["sub"]),
            email=email,
            full_name=data.get("name") or email.split("@")[0],
        )

    async def _profile_facebook(self, access_token: str) -> OAuthProfile:
        async with httpx.AsyncClient(timeout=30.0) as client:
            response = await client.get(
                "https://graph.facebook.com/me",
                params={"fields": "id,name,email", "access_token": access_token},
            )
            response.raise_for_status()
            data = response.json()
        email = (data.get("email") or "").lower()
        if not email:
            raise OAuthError("Facebook n'a pas fourni d'adresse email")
        return OAuthProfile(
            provider="facebook",
            provider_user_id=str(data["id"]),
            email=email,
            full_name=data.get("name") or email.split("@")[0],
        )

    async def _profile_github(self, access_token: str) -> OAuthProfile:
        headers = {
            "Authorization": f"Bearer {access_token}",
            "Accept": "application/vnd.github+json",
        }
        async with httpx.AsyncClient(timeout=30.0) as client:
            user_resp = await client.get("https://api.github.com/user", headers=headers)
            user_resp.raise_for_status()
            data = user_resp.json()
            email = (data.get("email") or "").lower()
            if not email:
                emails_resp = await client.get("https://api.github.com/user/emails", headers=headers)
                emails_resp.raise_for_status()
                for item in emails_resp.json():
                    if item.get("primary") and item.get("verified"):
                        email = item["email"].lower()
                        break
                if not email:
                    for item in emails_resp.json():
                        if item.get("verified"):
                            email = item["email"].lower()
                            break
        if not email:
            raise OAuthError("GitHub n'a pas fourni d'adresse email vérifiée")
        return OAuthProfile(
            provider="github",
            provider_user_id=str(data["id"]),
            email=email,
            full_name=data.get("name") or data.get("login") or email.split("@")[0],
        )

    async def _profile_microsoft(self, access_token: str) -> OAuthProfile:
        async with httpx.AsyncClient(timeout=30.0) as client:
            response = await client.get(
                "https://graph.microsoft.com/v1.0/me",
                headers={"Authorization": f"Bearer {access_token}"},
            )
            response.raise_for_status()
            data = response.json()
        email = (data.get("mail") or data.get("userPrincipalName") or "").lower()
        if not email:
            raise OAuthError("Microsoft n'a pas fourni d'adresse email")
        display = data.get("displayName") or email.split("@")[0]
        return OAuthProfile(
            provider="microsoft",
            provider_user_id=str(data["id"]),
            email=email,
            full_name=display,
        )

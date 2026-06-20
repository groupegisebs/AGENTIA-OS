"""Charge les secrets serveur depuis secrets.json (modèle GiseBsPayGateway)."""

from __future__ import annotations

import json
import os
from pathlib import Path
from typing import Any

SECRETS_FILE_ENV_VAR = "AGENTIA_SECRETS_FILE"


def resolve_secrets_file_path() -> Path | None:
    from_env = os.environ.get(SECRETS_FILE_ENV_VAR, "").strip()
    if from_env:
        path = Path(from_env)
        return path if path.is_file() else None
    return None


def _section(data: dict[str, Any], *names: str) -> dict[str, Any]:
    for name in names:
        block = data.get(name)
        if isinstance(block, dict):
            return block
    return {}


def load_server_secret_overrides() -> dict[str, Any]:
    """Retourne des champs Settings à surcharger depuis secrets.json."""
    path = resolve_secrets_file_path()
    if path is None:
        return {}

    try:
        data = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError):
        return {}

    overrides: dict[str, Any] = {}

    jwt = _section(data, "Jwt", "jwt")
    if secret := jwt.get("SecretKey") or jwt.get("secret_key"):
        overrides["jwt_secret"] = str(secret)

    gemini = _section(data, "Gemini", "gemini")
    if key := gemini.get("ApiKey") or gemini.get("api_key"):
        overrides["gemini_api_key"] = str(key)
    if model := gemini.get("Model") or gemini.get("model"):
        overrides["gemini_model"] = str(model)

    openai = _section(data, "OpenAI", "openai")
    if key := openai.get("ApiKey") or openai.get("api_key"):
        overrides["openai_api_key"] = str(key)

    gisebs = _section(data, "GiseBsPay", "GiseBsPayGateway", "gisebs_pay")
    if url := gisebs.get("GatewayUrl") or gisebs.get("gateway_url"):
        overrides["gisebs_pay_gateway_url"] = str(url)
    if key := gisebs.get("ApiKey") or gisebs.get("api_key"):
        overrides["gisebs_pay_api_key"] = str(key)
    if code := gisebs.get("AppCode") or gisebs.get("app_code"):
        overrides["gisebs_pay_app_code"] = str(code)

    oauth = _section(data, "OAuth", "oauth")
    if base := oauth.get("RedirectBaseUrl") or oauth.get("redirect_base_url"):
        overrides["oauth_redirect_base_url"] = str(base).rstrip("/")

    def _provider_block(*names: str) -> dict[str, Any]:
        for name in names:
            block = oauth.get(name)
            if isinstance(block, dict):
                return block
        return {}

    google = _provider_block("Google", "google")
    if cid := google.get("ClientId") or google.get("client_id"):
        overrides["oauth_google_client_id"] = str(cid)
    if secret := google.get("ClientSecret") or google.get("client_secret"):
        overrides["oauth_google_client_secret"] = str(secret)

    facebook = _provider_block("Facebook", "facebook")
    if app_id := facebook.get("AppId") or facebook.get("app_id"):
        overrides["oauth_facebook_app_id"] = str(app_id)
    if secret := facebook.get("AppSecret") or facebook.get("app_secret"):
        overrides["oauth_facebook_app_secret"] = str(secret)

    github = _provider_block("GitHub", "github")
    if cid := github.get("ClientId") or github.get("client_id"):
        overrides["oauth_github_client_id"] = str(cid)
    if secret := github.get("ClientSecret") or github.get("client_secret"):
        overrides["oauth_github_client_secret"] = str(secret)

    microsoft = _provider_block("Microsoft", "microsoft")
    if cid := microsoft.get("ClientId") or microsoft.get("client_id"):
        overrides["oauth_microsoft_client_id"] = str(cid)
    if secret := microsoft.get("ClientSecret") or microsoft.get("client_secret"):
        overrides["oauth_microsoft_client_secret"] = str(secret)
    if tenant := microsoft.get("TenantId") or microsoft.get("tenant_id"):
        overrides["oauth_microsoft_tenant_id"] = str(tenant)

    return overrides

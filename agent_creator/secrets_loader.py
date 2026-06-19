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

    return overrides

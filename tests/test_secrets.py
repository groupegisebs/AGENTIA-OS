import json
import sys
from pathlib import Path

import pytest

ROOT = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(ROOT / "deploy"))

from normalize_db_url import normalize_database_url  # noqa: E402


def test_normalize_dotnet_connection_string() -> None:
    url = normalize_database_url(
        "Host=51.79.53.197;Port=5432;Database=agentia;Username=gisedocuser;Password=secret"
    )
    assert url.startswith("postgresql+asyncpg://")
    assert "gisedocuser" in url
    assert "agentia" in url


def test_normalize_sqlalchemy_url_unchanged() -> None:
    raw = "postgresql+asyncpg://u:p@127.0.0.1:5432/agentia"
    assert normalize_database_url(raw) == raw


@pytest.mark.asyncio
async def test_settings_loads_server_secrets_json(tmp_path, monkeypatch) -> None:
    secrets = tmp_path / "secrets.json"
    secrets.write_text(
        json.dumps(
            {
                "Jwt": {"SecretKey": "x" * 40},
                "Gemini": {"ApiKey": "test-gemini-key"},
            }
        ),
        encoding="utf-8",
    )
    monkeypatch.setenv("AGENTIA_SECRETS_FILE", str(secrets))
    from agent_creator.config import get_settings

    get_settings.cache_clear()
    settings = get_settings()
    assert settings.jwt_secret == "x" * 40
    assert settings.gemini_api_key == "test-gemini-key"
    get_settings.cache_clear()


def test_settings_loads_oauth_from_secrets_json(tmp_path, monkeypatch) -> None:
    secrets = tmp_path / "secrets.json"
    secrets.write_text(
        json.dumps(
            {
                "OAuth": {
                    "RedirectBaseUrl": "https://app.example.com",
                    "Google": {"ClientId": "g-id", "ClientSecret": "g-secret"},
                }
            }
        ),
        encoding="utf-8",
    )
    monkeypatch.setenv("AGENTIA_SECRETS_FILE", str(secrets))
    from agent_creator.config import get_settings

    get_settings.cache_clear()
    settings = get_settings()
    assert settings.oauth_redirect_base_url == "https://app.example.com"
    assert settings.oauth_google_client_id == "g-id"
    assert settings.oauth_google_client_secret == "g-secret"
    get_settings.cache_clear()

import pytest

from agent_creator.config import Settings
from agent_creator.services.auth import AuthService
from agent_creator.services.oauth import OAuthError, OAuthProfile, OAuthService, list_enabled_providers


def test_list_enabled_providers_empty() -> None:
    settings = Settings()
    assert list_enabled_providers(settings) == []


def test_list_enabled_providers_partial() -> None:
    settings = Settings(
        oauth_google_client_id="google-id",
        oauth_google_client_secret="google-secret",
        oauth_github_client_id="gh-id",
        oauth_github_client_secret="gh-secret",
    )
    assert list_enabled_providers(settings) == ["google", "github"]


@pytest.mark.asyncio
async def test_oauth_providers_endpoint(client) -> None:
    res = await client.get("/auth/oauth/providers")
    assert res.status_code == 200
    assert res.json()["providers"] == []


@pytest.mark.asyncio
async def test_oauth_start_unconfigured(client) -> None:
    res = await client.get("/auth/oauth/google", follow_redirects=False)
    assert res.status_code == 503


@pytest.mark.asyncio
async def test_oauth_login_or_register_creates_account(client) -> None:
    from agent_creator.db.session import async_session_factory

    auth = AuthService(Settings(jwt_secret="test-secret-key-for-tests-only"))
    profile = OAuthProfile(
        provider="google",
        provider_user_id="google-user-123",
        email="oauth@example.com",
        full_name="OAuth User",
    )
    factory = async_session_factory()
    session = factory()
    try:
        user, org, token = await auth.oauth_login_or_register(session, profile)
        await session.commit()
    finally:
        await session.close()

    assert user.email == "oauth@example.com"
    assert org.name == "Organisation de OAuth User"
    assert token

    me = await client.get("/auth/me", headers={"Authorization": f"Bearer {token}"})
    assert me.status_code == 200
    assert me.json()["user"]["email"] == "oauth@example.com"


@pytest.mark.asyncio
async def test_oauth_links_existing_email_account(client) -> None:
    from agent_creator.db.session import async_session_factory

    reg = await client.post(
        "/auth/register",
        json={
            "email": "link@example.com",
            "password": "password123",
            "full_name": "Link User",
            "organization_name": "Link Org",
        },
    )
    assert reg.status_code == 201

    auth = AuthService(Settings(jwt_secret="test-secret-key-for-tests-only"))
    profile = OAuthProfile(
        provider="github",
        provider_user_id="github-999",
        email="link@example.com",
        full_name="GitHub Name",
    )
    factory = async_session_factory()
    session = factory()
    try:
        user, org, token = await auth.oauth_login_or_register(session, profile)
        await session.commit()
    finally:
        await session.close()

    assert user.email == "link@example.com"
    assert org.name == "Link Org"
    assert token

    session2 = factory()
    try:
        user2, org2, token2 = await auth.oauth_login_or_register(session2, profile)
        await session2.commit()
    finally:
        await session2.close()
    assert user2.email == "link@example.com"
    assert org2.name == "Link Org"
    assert token2


def test_oauth_state_roundtrip() -> None:
    settings = Settings(jwt_secret="test-secret-key-for-tests-only")
    oauth = OAuthService(settings)
    state = oauth.create_state_token("google")
    oauth.verify_state_token("google", state)

    with pytest.raises(OAuthError):
        oauth.verify_state_token("facebook", state)

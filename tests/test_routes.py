import pytest


@pytest.mark.asyncio
async def test_spa_connexion_route(client) -> None:
    res = await client.get("/connexion")
    assert res.status_code == 200
    assert "text/html" in res.headers.get("content-type", "")
    assert "Agentia" in res.text


@pytest.mark.asyncio
async def test_spa_inscription_route(client) -> None:
    res = await client.get("/inscription")
    assert res.status_code == 200
    assert "text/html" in res.headers.get("content-type", "")


@pytest.mark.asyncio
async def test_auth_register_route_exists(client) -> None:
    """La route API doit exister (422/400 = validation, pas 404)."""
    res = await client.post("/auth/register", json={})
    assert res.status_code != 404, "POST /auth/register introuvable — vérifier le déploiement API"

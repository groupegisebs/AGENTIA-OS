def test_app_imports() -> None:
    from agent_creator.main import app

    assert app.title.startswith("Agentia")

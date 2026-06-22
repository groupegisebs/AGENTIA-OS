"""Tests du manifest builder — blueprint → AgentManifest."""

import pytest

from agent_creator.models.blueprint import Blueprint, BlueprintComponent
from agent_creator.models.requirement import Requirement, SolutionType
from agent_creator.models.subscription import SubscriptionPlan
from agent_creator.services.manifest_builder import build_manifest


def _make_blueprint(domain: str, objectives: list[str], solution_type: SolutionType = SolutionType.WORKFLOW) -> Blueprint:
    return Blueprint(
        conversation_id="conv-test",
        title="Test Solution",
        solution_type=solution_type,
        solution_type_rationale="test",
        requirements=Requirement(
            objectives=objectives,
            domain=domain,
            completeness_score=0.8,
            summary="Résumé test",
        ),
        components=[
            BlueprintComponent(name="Orchestrateur", type="workflow", description="Coordination"),
            BlueprintComponent(name="Agent IA", type="ai", description="Traitement IA"),
        ],
    )


def _fake_settings():
    from agent_creator.config import Settings
    return Settings(
        database_url="sqlite+aiosqlite:///:memory:",
        jwt_secret="test",
        gemini_api_key="",
        openai_api_key="",
    )


def test_manifest_has_system_prompt():
    bp = _make_blueprint("comptabilité", ["Automatiser les factures"])
    manifest = build_manifest(bp, SubscriptionPlan.PROFESSIONAL, _fake_settings())
    assert "Automatiser les factures" in manifest.system_prompt
    assert "comptabilité" in manifest.system_prompt.lower() or "comptab" in manifest.system_prompt.lower()


def test_manifest_category_inferred_from_domain():
    bp = _make_blueprint("comptabilité cabinet", ["traitements"])
    manifest = build_manifest(bp, SubscriptionPlan.FREE, _fake_settings())
    assert manifest.category in ("Comptabilité", "Finance", "Général")


def test_manifest_policies_by_plan():
    bp = _make_blueprint("RH", ["gestion employés"])
    free_manifest = build_manifest(bp, SubscriptionPlan.FREE, _fake_settings())
    pro_manifest = build_manifest(bp, SubscriptionPlan.PROFESSIONAL, _fake_settings())
    assert pro_manifest.policies.max_input_chars > free_manifest.policies.max_input_chars
    assert pro_manifest.policies.max_requests_per_hour > free_manifest.policies.max_requests_per_hour


def test_manifest_pii_filter_always_on():
    bp = _make_blueprint("juridique", ["analyser contrats"])
    manifest = build_manifest(bp, SubscriptionPlan.ENTERPRISE, _fake_settings())
    assert manifest.policies.pii_filter is True


def test_manifest_components_serialised():
    bp = _make_blueprint("crm", ["suivre prospects"])
    manifest = build_manifest(bp, SubscriptionPlan.FREE, _fake_settings())
    assert len(manifest.components) == 2
    names = [c["name"] for c in manifest.components]
    assert "Orchestrateur" in names
    assert "Agent IA" in names


def test_manifest_category_crm():
    bp = _make_blueprint("crm", ["suivre prospects et relances"])
    manifest = build_manifest(bp, SubscriptionPlan.FREE, _fake_settings())
    assert manifest.category == "CRM"

from enum import Enum

from pydantic import BaseModel, Field


class SolutionType(str, Enum):
    WORKFLOW = "workflow"
    AGENT = "agent"
    API = "api"
    MICROSERVICE = "microservice"
    HYBRID = "hybrid"


class Requirement(BaseModel):
    """Exigences structurées extraites du dialogue."""

    objectives: list[str] = Field(default_factory=list, description="Objectifs métier identifiés")
    constraints: list[str] = Field(default_factory=list, description="Contraintes techniques ou organisationnelles")
    data_sources: list[str] = Field(default_factory=list, description="Sources de données mentionnées")
    volumes: list[str] = Field(default_factory=list, description="Volumes, fréquences, SLA")
    risks: list[str] = Field(default_factory=list, description="Risques et points d'attention")
    domain: str | None = Field(default=None, description="Domaine métier principal")
    summary: str | None = Field(default=None, description="Résumé synthétique du besoin")
    completeness_score: float = Field(
        default=0.0,
        ge=0.0,
        le=1.0,
        description="Score de complétude des informations (0-1)",
    )
    missing_information: list[str] = Field(
        default_factory=list,
        description="Informations manquantes pour finaliser le blueprint",
    )

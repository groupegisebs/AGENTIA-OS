from datetime import datetime
from uuid import uuid4

from pydantic import BaseModel, Field

from agent_creator.models.requirement import Requirement, SolutionType


class BlueprintComponent(BaseModel):
    name: str
    type: str
    description: str
    technology_hint: str | None = None


class Blueprint(BaseModel):
    """Plan d'architecture de solution généré à partir des exigences."""

    id: str = Field(default_factory=lambda: str(uuid4()))
    conversation_id: str
    title: str
    solution_type: SolutionType
    solution_type_rationale: str
    secondary_types: list[SolutionType] = Field(default_factory=list)
    requirements: Requirement
    components: list[BlueprintComponent] = Field(default_factory=list)
    data_flow: list[str] = Field(default_factory=list, description="Étapes du flux de données")
    clarifying_questions: list[str] = Field(default_factory=list)
    next_steps: list[str] = Field(default_factory=list)
    confidence: float = Field(default=0.0, ge=0.0, le=1.0)
    generated_at: datetime = Field(default_factory=datetime.utcnow)

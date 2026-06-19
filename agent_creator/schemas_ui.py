from pydantic import BaseModel, Field


class EstimatesResponse(BaseModel):
    complexity: str
    complexity_score: float
    build_time_min: int
    monthly_cost_eur: float
    hours_saved_per_month: float
    roi_percent: int
    ready: bool


class ArchitectAnalyzeRequest(BaseModel):
    description: str = Field(..., min_length=10, description="Description de l'activité ou du contexte métier")


class ArchitectProposalResponse(BaseModel):
    title: str
    description: str
    need: str


class ArchitectAnalyzeResponse(BaseModel):
    processes_count: int
    hours_saved_per_year: float
    monthly_cost_eur: float
    roi_percent: int
    proposals: list[ArchitectProposalResponse]
    summary: str

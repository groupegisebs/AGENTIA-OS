from fastapi import APIRouter

from agent_creator.schemas_ui import ArchitectAnalyzeRequest, ArchitectAnalyzeResponse, ArchitectProposalResponse
from agent_creator.services.architect_analyzer import analyze_business

router = APIRouter(prefix="/architect", tags=["architecte"])


@router.post("/analyze", response_model=ArchitectAnalyzeResponse)
async def analyze_business_context(body: ArchitectAnalyzeRequest) -> ArchitectAnalyzeResponse:
    """Analyse un contexte métier et propose des solutions automatisables (MVP rule-based)."""
    result = analyze_business(body.description)
    return ArchitectAnalyzeResponse(
        processes_count=result["processes_count"],
        hours_saved_per_year=result["hours_saved_per_year"],
        monthly_cost_eur=result["monthly_cost_eur"],
        roi_percent=result["roi_percent"],
        proposals=[ArchitectProposalResponse(**p) for p in result["proposals"]],
        summary=result["summary"],
    )

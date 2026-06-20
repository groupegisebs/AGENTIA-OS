from fastapi import APIRouter, Depends

from agent_creator.dependencies import UserContext, get_current_user
from agent_creator.schemas_ui import ArchitectAnalyzeRequest, ArchitectAnalyzeResponse, ArchitectProposalResponse
from agent_creator.services.architect_analyzer import analyze_business

router = APIRouter(prefix="/architect", tags=["architecte"])


@router.post("/analyze", response_model=ArchitectAnalyzeResponse)
async def analyze_business_context(
    body: ArchitectAnalyzeRequest,
    _ctx: UserContext = Depends(get_current_user),
) -> ArchitectAnalyzeResponse:
    """Analyse un contexte métier — authentification requise."""
    result = analyze_business(body.description)
    return ArchitectAnalyzeResponse(
        processes_count=result["processes_count"],
        hours_saved_per_year=result["hours_saved_per_year"],
        monthly_cost_eur=result["monthly_cost_eur"],
        roi_percent=result["roi_percent"],
        proposals=[ArchitectProposalResponse(**p) for p in result["proposals"]],
        summary=result["summary"],
    )

from fastapi import APIRouter, Depends

from agent_creator.dependencies import UserContext, get_current_user, get_deployment_service
from agent_creator.schemas_billing import PlanResponse
from agent_creator.services.deployment import DeploymentService

router = APIRouter(prefix="/plans", tags=["abonnements"])


@router.get("", response_model=list[PlanResponse])
async def list_subscription_plans(
    _ctx: UserContext = Depends(get_current_user),
    service: DeploymentService = Depends(get_deployment_service),
) -> list[PlanResponse]:
    """Liste les plans d'abonnement — authentification requise."""
    return [PlanResponse.from_config(p) for p in service.get_plans()]

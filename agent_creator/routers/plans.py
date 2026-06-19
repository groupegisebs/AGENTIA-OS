from fastapi import APIRouter, Depends

from agent_creator.schemas_billing import PlanResponse
from agent_creator.services.deployment import DeploymentService

router = APIRouter(prefix="/plans", tags=["abonnements"])


def get_deployment_service() -> DeploymentService:
    from agent_creator.main import deployment_service

    return deployment_service


@router.get("", response_model=list[PlanResponse])
async def list_subscription_plans(
    service: DeploymentService = Depends(get_deployment_service),
) -> list[PlanResponse]:
    """Liste les plans d'abonnement disponibles (Gratuit, Professionnel, Business, Entreprise)."""
    return [PlanResponse.from_config(p) for p in service.get_plans()]

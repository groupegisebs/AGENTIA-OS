from fastapi import APIRouter, Depends, HTTPException

from agent_creator.models.subscription import SubscriptionPlan
from agent_creator.schemas_billing import (
    BillingEventResponse,
    BillingSummaryResponse,
    DeploymentResponse,
    OrganizationDetailResponse,
    OrganizationResponse,
    SubscribeRequest,
    SubscribeResponse,
)
from agent_creator.services.billing import BillingService
from agent_creator.services.deployment import DeploymentService

router = APIRouter(prefix="/organizations", tags=["organisations"])


def get_deployment_service() -> DeploymentService:
    from agent_creator.main import deployment_service

    return deployment_service


def get_billing_service() -> BillingService:
    from agent_creator.main import billing_service

    return billing_service


def get_org_store():
    from agent_creator.main import org_store

    return org_store


def get_default_org_id() -> str:
    from agent_creator.main import org_store

    return org_store.default_org_id


@router.get("/me", response_model=OrganizationDetailResponse)
async def get_current_organization(
    service: DeploymentService = Depends(get_deployment_service),
    default_org_id: str = Depends(get_default_org_id),
) -> OrganizationDetailResponse:
    """Retourne l'organisation courante (tenant par défaut du MVP)."""
    return await _build_org_detail(default_org_id, service)


@router.get("/{organization_id}", response_model=OrganizationDetailResponse)
async def get_organization(
    organization_id: str,
    service: DeploymentService = Depends(get_deployment_service),
) -> OrganizationDetailResponse:
    """Détail d'une organisation et de son abonnement."""
    org = service.get_organization(organization_id)
    if not org:
        raise HTTPException(status_code=404, detail="Organisation introuvable")
    return await _build_org_detail(organization_id, service)


@router.get("/{organization_id}/billing", response_model=BillingSummaryResponse)
async def get_billing_history(
    organization_id: str,
    service: DeploymentService = Depends(get_deployment_service),
) -> BillingSummaryResponse:
    """Historique des déploiements et événements de facturation."""
    try:
        summary = service.get_billing_summary(organization_id)
    except ValueError:
        raise HTTPException(status_code=404, detail="Organisation introuvable") from None

    return BillingSummaryResponse(
        organization=OrganizationResponse.from_organization(summary["organization"]),
        plan_name=summary["plan_config"].name,
        deployments_used_this_month=summary["deployments_used_this_month"],
        deployments_limit=summary["deployments_limit"],
        total_billed=summary["total_billed_eur"],
        deployments=[DeploymentResponse.from_deployment(d) for d in summary["deployments"]],
        billing_events=[BillingEventResponse.from_event(e) for e in summary["billing_events"]],
    )


@router.get("/{organization_id}/deployments", response_model=list[DeploymentResponse])
async def list_deployments(
    organization_id: str,
    service: DeploymentService = Depends(get_deployment_service),
) -> list[DeploymentResponse]:
    """Liste les déploiements d'une organisation."""
    try:
        summary = service.get_billing_summary(organization_id)
    except ValueError:
        raise HTTPException(status_code=404, detail="Organisation introuvable") from None

    return [DeploymentResponse.from_deployment(d) for d in summary["deployments"]]


@router.post("/{organization_id}/subscribe", response_model=SubscribeResponse)
async def subscribe_organization(
    organization_id: str,
    body: SubscribeRequest,
    billing: BillingService = Depends(get_billing_service),
    org_store=Depends(get_org_store),
) -> SubscribeResponse:
    """Crée une session de paiement d'abonnement via GiseBsPayGateway."""
    organization = org_store.get(organization_id)
    if not organization:
        raise HTTPException(status_code=404, detail="Organisation introuvable")

    result = await billing.create_subscription_checkout(organization, body.plan)
    if not result.success:
        raise HTTPException(status_code=400, detail=result.error_message or "Échec de l'abonnement")

    if body.plan == SubscriptionPlan.FREE:
        org_store.save(organization)
        return SubscribeResponse(
            plan=body.plan,
            success=True,
            message="Plan gratuit activé.",
        )

    return SubscribeResponse(
        plan=body.plan,
        success=True,
        message="Session d'abonnement créée — finalisez le paiement sur GiseBsPayGateway.",
        checkout_url=result.checkout_url,
        payment_code=result.payment_code,
        client_secret=result.client_secret,
        publishable_key=result.publishable_key,
    )


async def _build_org_detail(organization_id: str, service: DeploymentService) -> OrganizationDetailResponse:
    try:
        summary = service.get_billing_summary(organization_id)
    except ValueError:
        raise HTTPException(status_code=404, detail="Organisation introuvable") from None

    org = summary["organization"]
    plan_config = summary["plan_config"]
    return OrganizationDetailResponse(
        id=org.id,
        name=org.name,
        plan=org.plan,
        billing_email=org.billing_email,
        created_at=org.created_at,
        plan_name=plan_config.name,
        deployments_used_this_month=summary["deployments_used_this_month"],
        deployments_limit=summary["deployments_limit"],
        monthly_subscription_eur=plan_config.monthly_price_eur,
    )

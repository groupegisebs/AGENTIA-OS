from fastapi import APIRouter, Depends, HTTPException

from agent_creator.dependencies import UserContext, get_current_user, get_db_store, get_deployment_service
from agent_creator.db.repository import DbStore
from agent_creator.models.subscription import SubscriptionPlan
from agent_creator.schemas_billing import (
    BillingEventResponse,
    BillingSummaryResponse,
    DeploymentResponse,
    OrganizationDetailResponse,
    OrganizationResponse,
    SubscribeConfirmRequest,
    SubscribeRequest,
    SubscribeResponse,
)
from agent_creator.services.billing import BillingService
from agent_creator.services.deployment import DeploymentService

router = APIRouter(prefix="/organizations", tags=["organisations"])


def get_billing_service() -> BillingService:
    from agent_creator.main import billing_service

    return billing_service


@router.get("/me", response_model=OrganizationDetailResponse)
async def get_current_organization(
    ctx: UserContext = Depends(get_current_user),
    service: DeploymentService = Depends(get_deployment_service),
) -> OrganizationDetailResponse:
    return await _build_org_detail(ctx.organization.id, service)


@router.get("/{organization_id}", response_model=OrganizationDetailResponse)
async def get_organization(
    organization_id: str,
    ctx: UserContext = Depends(get_current_user),
    service: DeploymentService = Depends(get_deployment_service),
) -> OrganizationDetailResponse:
    if organization_id != ctx.organization.id:
        raise HTTPException(status_code=403, detail="Accès refusé")
    return await _build_org_detail(organization_id, service)


@router.get("/{organization_id}/billing", response_model=BillingSummaryResponse)
async def get_billing_history(
    organization_id: str,
    ctx: UserContext = Depends(get_current_user),
    service: DeploymentService = Depends(get_deployment_service),
) -> BillingSummaryResponse:
    if organization_id != ctx.organization.id:
        raise HTTPException(status_code=403, detail="Accès refusé")
    try:
        summary = await service.get_billing_summary(organization_id)
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
    ctx: UserContext = Depends(get_current_user),
    service: DeploymentService = Depends(get_deployment_service),
) -> list[DeploymentResponse]:
    if organization_id != ctx.organization.id:
        raise HTTPException(status_code=403, detail="Accès refusé")
    try:
        summary = await service.get_billing_summary(organization_id)
    except ValueError:
        raise HTTPException(status_code=404, detail="Organisation introuvable") from None

    return [DeploymentResponse.from_deployment(d) for d in summary["deployments"]]


@router.post("/me/subscribe", response_model=SubscribeResponse)
async def subscribe_current_org(
    body: SubscribeRequest,
    ctx: UserContext = Depends(get_current_user),
    db: DbStore = Depends(get_db_store),
    billing: BillingService = Depends(get_billing_service),
) -> SubscribeResponse:
    """Crée une session de paiement d'abonnement pour l'organisation connectée."""
    organization = ctx.organization
    result, payment_intent = await billing.create_subscription_checkout(db, organization, body.plan)
    if not result.success:
        raise HTTPException(status_code=400, detail=result.error_message or "Échec de l'abonnement")

    if body.plan == SubscriptionPlan.FREE:
        return SubscribeResponse(plan=body.plan, success=True, message="Plan gratuit activé.")

    if payment_intent:
        await db.save_payment_intent(payment_intent)

    return SubscribeResponse(
        plan=body.plan,
        success=True,
        message="Session d'abonnement créée — finalisez le paiement.",
        checkout_url=result.checkout_url,
        payment_code=result.payment_code,
        client_secret=result.client_secret,
        publishable_key=result.publishable_key,
    )


@router.post("/me/subscribe/confirm", response_model=SubscribeResponse)
async def confirm_subscription(
    body: SubscribeConfirmRequest,
    ctx: UserContext = Depends(get_current_user),
    db: DbStore = Depends(get_db_store),
    billing: BillingService = Depends(get_billing_service),
) -> SubscribeResponse:
    """Confirme un abonnement après paiement GiseBsPayGateway."""
    try:
        org = await billing.confirm_subscription_payment(
            db, ctx.organization, body.payment_code, body.plan
        )
    except ValueError as exc:
        raise HTTPException(status_code=400, detail=str(exc)) from exc

    return SubscribeResponse(
        plan=org.plan,
        success=True,
        message=f"Abonnement {org.plan.value} activé.",
    )


@router.post("/{organization_id}/subscribe", response_model=SubscribeResponse)
async def subscribe_organization(
    organization_id: str,
    body: SubscribeRequest,
    ctx: UserContext = Depends(get_current_user),
    db: DbStore = Depends(get_db_store),
    billing: BillingService = Depends(get_billing_service),
) -> SubscribeResponse:
    if organization_id != ctx.organization.id:
        raise HTTPException(status_code=403, detail="Accès refusé")
    return await subscribe_current_org(body, ctx, db, billing)


async def _build_org_detail(organization_id: str, service: DeploymentService) -> OrganizationDetailResponse:
    try:
        summary = await service.get_billing_summary(organization_id)
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

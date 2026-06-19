from fastapi import APIRouter, Depends, HTTPException

from agent_creator.dependencies import UserContext, get_current_user, get_db_store
from agent_creator.db.repository import DbStore
from agent_creator.schemas_auth import ConfirmBillingRequest
from agent_creator.schemas_billing import BillingEventResponse, DeployResponse, DeploymentResponse, SubscribeResponse
from agent_creator.services.billing import BillingService
from agent_creator.services.payment import create_payment_provider
from agent_creator.config import get_settings

router = APIRouter(prefix="/billing", tags=["facturation"])


def get_billing_service() -> BillingService:
    from agent_creator.main import billing_service

    return billing_service


@router.post("/confirm")
async def confirm_billing(
    body: ConfirmBillingRequest,
    ctx: UserContext = Depends(get_current_user),
    db: DbStore = Depends(get_db_store),
    billing: BillingService = Depends(get_billing_service),
) -> dict:
    """Confirme un paiement GiseBsPayGateway (abonnement ou déploiement)."""
    try:
        result = await billing.confirm_payment_unified(db, ctx.organization, body.payment_code)
    except ValueError as exc:
        raise HTTPException(status_code=400, detail=str(exc)) from exc

    if result["type"] == "subscribe":
        org = result["organization"]
        return {
            "success": True,
            "type": "subscribe",
            "plan": org.plan.value,
            "message": f"Abonnement {org.plan.value} activé avec succès.",
        }

    deployment = result["deployment"]
    billing_event = result["billing_event"]
    return {
        "success": True,
        "type": "deploy",
        "message": f"Déploiement confirmé — {deployment.deployment_cost:.2f} {deployment.currency} facturés.",
        "deployment": DeploymentResponse.from_deployment(deployment),
        "billing_event": BillingEventResponse.from_event(billing_event),
    }


@router.get("/payments/{payment_code}/status")
async def payment_status(
    payment_code: str,
    ctx: UserContext = Depends(get_current_user),
    db: DbStore = Depends(get_db_store),
) -> dict:
    """Statut d'une intention de paiement (polling frontend)."""
    intent = await db.get_payment_intent_by_code(payment_code)
    if not intent or intent.organization_id != ctx.organization.id:
        raise HTTPException(status_code=404, detail="Paiement introuvable")

    settings = get_settings()
    provider = create_payment_provider(settings)
    result = await provider.confirm_payment(payment_code)

    return {
        "payment_code": payment_code,
        "intent_type": intent.intent_type,
        "intent_status": intent.status,
        "paid": result.success,
        "pending": result.pending if hasattr(result, "pending") else not result.success,
    }

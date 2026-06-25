from fastapi import APIRouter, Depends, HTTPException

from agent_creator.dependencies import UserContext, get_current_user, get_db_store, get_deployment_service
from agent_creator.db.repository import DbStore
from agent_creator.models.conversation import Conversation, ConversationStatus, MessageRole
from agent_creator.schemas import (
    AssistantReplyResponse,
    BlueprintResponse,
    ConversationResponse,
    CreateConversationRequest,
    MessageResponse,
    SendMessageRequest,
    SolutionMetadata,
)
from agent_creator.schemas_ui import EstimatesResponse
from agent_creator.schemas_billing import BillingEventResponse, ConfirmPaymentRequest, DeployResponse, DeploymentResponse
from agent_creator.services.billing import BillingService, DeploymentLimitExceeded
from agent_creator.services.blueprint_generator import BlueprintGenerator
from agent_creator.services.deployment import DeploymentService
from agent_creator.services.estimates import build_estimates
from agent_creator.services.llm import LLMService

router = APIRouter(prefix="/conversations", tags=["conversations"])


def get_llm() -> LLMService:
    from agent_creator.main import llm

    return llm


def get_blueprint_generator() -> BlueprintGenerator:
    from agent_creator.main import blueprint_generator

    return blueprint_generator


def get_billing_service() -> BillingService:
    from agent_creator.main import billing_service

    return billing_service


def _llm_mode_label(llm: LLMService) -> str:
    return llm.mode_label


async def _assistant_reply(conversation: Conversation, llm: LLMService) -> str:
    messages = [{"role": "system", "content": llm.SYSTEM_PROMPT}]
    for msg in conversation.messages:
        messages.append({"role": msg.role.value, "content": msg.content})
    return await llm.chat(messages)


# Named systems/tools to detect in data sources
_KNOWN_SYSTEMS = [
    "gmail", "outlook", "exchange", "microsoft 365", "office 365",
    "sage", "quickbooks", "cegid", "ebp", "pennylane",
    "salesforce", "hubspot", "pipedrive", "zoho", "dynamics",
    "sap", "oracle", "odoo", "netsuite",
    "slack", "teams", "notion", "jira", "trello", "asana",
    "shopify", "woocommerce", "prestashop",
    "stripe", "paypal",
    "sharepoint", "onedrive", "google drive", "dropbox",
    "mysql", "postgresql", "mongodb", "supabase", "airtable",
    "zapier", "n8n", "power automate", "make",
]

_DOC_KEYWORDS = ["pdf", "facture", "contrat", "devis", "bon de commande",
                 "rapport", "fiche", "document", "fichier", "pièce jointe",
                 "excel", "csv", "word"]

_USER_KEYWORDS = ["comptable", "directeur", "manager", "rh", "commercial",
                  "client", "employé", "collaborateur", "utilisateur", "équipe",
                  "responsable", "chef", "secrétaire", "assistant"]


async def _extract_metadata(conversation: Conversation, llm: LLMService) -> SolutionMetadata:
    """Extrait les métadonnées de la solution depuis la conversation."""
    from agent_creator.services.extractor import RequirementExtractor

    extractor = RequirementExtractor(llm)
    req = await extractor.extract(conversation)

    full_text = conversation.transcript.lower()

    # Systems: named tools detected in data_sources + text scan
    systems: list[str] = []
    src_text = " ".join(req.data_sources).lower() + " " + full_text
    for sys in _KNOWN_SYSTEMS:
        if sys in src_text and sys.title() not in systems:
            systems.append(sys.title())

    # Documents: file/document mentions in data sources and transcript
    documents: list[str] = []
    for ds in req.data_sources:
        ds_lower = ds.lower()
        if any(kw in ds_lower for kw in _DOC_KEYWORDS):
            if ds not in documents:
                documents.append(ds)
    # Scan transcript for document mentions not in data_sources
    for kw in _DOC_KEYWORDS:
        if kw in full_text and not any(kw in d.lower() for d in documents):
            documents.append(kw.capitalize())

    # Users: role mentions in transcript
    users: list[str] = []
    for kw in _USER_KEYWORDS:
        if kw in full_text and kw.capitalize() not in users:
            users.append(kw.capitalize())

    return SolutionMetadata(
        objectives=req.objectives[:6],
        systems=systems[:8],
        documents=list(dict.fromkeys(documents))[:6],
        users=users[:6],
        constraints=req.constraints[:5],
        risks=req.risks[:4],
        completeness=req.completeness_score,
    )


@router.post("", response_model=AssistantReplyResponse, status_code=201)
async def create_conversation(
    body: CreateConversationRequest,
    ctx: UserContext = Depends(get_current_user),
    db: DbStore = Depends(get_db_store),
    llm: LLMService = Depends(get_llm),
) -> AssistantReplyResponse:
    conversation = Conversation(organization_id=ctx.organization.id)
    conversation.add_message(MessageRole.USER, body.message)

    reply = await _assistant_reply(conversation, llm)
    assistant_msg = conversation.add_message(MessageRole.ASSISTANT, reply)

    metadata = await _extract_metadata(conversation, llm)
    conversation.clarifying_questions = metadata.constraints[:5] or []

    await db.create_conversation(conversation)
    mode = _llm_mode_label(llm)
    return AssistantReplyResponse(
        conversation=ConversationResponse.from_conversation(conversation, mode),
        assistant_message=MessageResponse(
            id=assistant_msg.id,
            role=assistant_msg.role,
            content=assistant_msg.content,
            created_at=assistant_msg.created_at,
        ),
        metadata=metadata,
    )


@router.get("", response_model=list[ConversationResponse])
async def list_conversations(
    ctx: UserContext = Depends(get_current_user),
    db: DbStore = Depends(get_db_store),
    llm: LLMService = Depends(get_llm),
) -> list[ConversationResponse]:
    mode = _llm_mode_label(llm)
    convos = await db.list_conversations(ctx.organization.id)
    return [ConversationResponse.from_conversation(c, mode) for c in convos]


@router.get("/{conversation_id}", response_model=ConversationResponse)
async def get_conversation(
    conversation_id: str,
    ctx: UserContext = Depends(get_current_user),
    db: DbStore = Depends(get_db_store),
    llm: LLMService = Depends(get_llm),
) -> ConversationResponse:
    conversation = await db.get_conversation(conversation_id, ctx.organization.id)
    if not conversation:
        raise HTTPException(status_code=404, detail="Conversation introuvable")
    return ConversationResponse.from_conversation(conversation, _llm_mode_label(llm))


@router.post("/{conversation_id}/messages", response_model=AssistantReplyResponse)
async def send_message(
    conversation_id: str,
    body: SendMessageRequest,
    ctx: UserContext = Depends(get_current_user),
    db: DbStore = Depends(get_db_store),
    llm: LLMService = Depends(get_llm),
) -> AssistantReplyResponse:
    conversation = await db.get_conversation(conversation_id, ctx.organization.id)
    if not conversation:
        raise HTTPException(status_code=404, detail="Conversation introuvable")

    conversation.add_message(MessageRole.USER, body.message)
    reply = await _assistant_reply(conversation, llm)
    assistant_msg = conversation.add_message(MessageRole.ASSISTANT, reply)

    metadata = await _extract_metadata(conversation, llm)

    # Mark ready when enough information gathered
    if metadata.completeness >= 0.65 or len(conversation.user_messages) >= 4:
        conversation.status = ConversationStatus.READY_FOR_BLUEPRINT

    await db.save_conversation(conversation)
    mode = _llm_mode_label(llm)
    return AssistantReplyResponse(
        conversation=ConversationResponse.from_conversation(conversation, mode),
        assistant_message=MessageResponse(
            id=assistant_msg.id,
            role=assistant_msg.role,
            content=assistant_msg.content,
            created_at=assistant_msg.created_at,
        ),
        metadata=metadata,
    )


@router.get("/{conversation_id}/estimates", response_model=EstimatesResponse)
async def get_estimates(
    conversation_id: str,
    ctx: UserContext = Depends(get_current_user),
    db: DbStore = Depends(get_db_store),
    billing: BillingService = Depends(get_billing_service),
) -> EstimatesResponse:
    conversation = await db.get_conversation(conversation_id, ctx.organization.id)
    if not conversation:
        raise HTTPException(status_code=404, detail="Conversation introuvable")

    blueprint = await db.get_blueprint(conversation_id)
    data = build_estimates(conversation, blueprint, ctx.organization, billing)
    return EstimatesResponse(**data)


@router.get("/{conversation_id}/blueprint", response_model=BlueprintResponse)
async def get_blueprint(
    conversation_id: str,
    ctx: UserContext = Depends(get_current_user),
    db: DbStore = Depends(get_db_store),
    llm: LLMService = Depends(get_llm),
    generator: BlueprintGenerator = Depends(get_blueprint_generator),
    billing: BillingService = Depends(get_billing_service),
) -> BlueprintResponse:
    conversation = await db.get_conversation(conversation_id, ctx.organization.id)
    if not conversation:
        raise HTTPException(status_code=404, detail="Conversation introuvable")

    if not conversation.user_messages:
        raise HTTPException(status_code=400, detail="La conversation ne contient aucun message utilisateur")

    existing = await db.get_blueprint(conversation_id)
    if existing:
        blueprint = existing
    else:
        blueprint = await generator.generate(conversation)
        conversation.status = ConversationStatus.BLUEPRINT_GENERATED
        await db.save_conversation(conversation)
        await db.save_blueprint(blueprint)

    deployment_hint = billing.estimate_deployment_cost_message(ctx.organization, blueprint)
    return BlueprintResponse(
        blueprint=blueprint,
        llm_mode=_llm_mode_label(llm),
        deployment_hint=deployment_hint,
    )


@router.post("/{conversation_id}/deploy", response_model=DeployResponse, status_code=201)
async def deploy_conversation(
    conversation_id: str,
    ctx: UserContext = Depends(get_current_user),
    deployment_service: DeploymentService = Depends(get_deployment_service),
) -> DeployResponse:
    try:
        deployment, billing_event = await deployment_service.deploy_blueprint(
            conversation_id, ctx.organization.id
        )
    except ValueError as exc:
        raise HTTPException(status_code=400, detail=str(exc)) from exc
    except DeploymentLimitExceeded as exc:
        raise HTTPException(
            status_code=402,
            detail={
                "message": str(exc),
                "deployments_used": exc.deployments_used,
                "limit": exc.limit,
            },
        ) from exc

    if billing_event is None:
        return DeployResponse(
            deployment=DeploymentResponse.from_deployment(deployment),
            billing_event=None,
            message="Cette solution est déjà déployée pour cette conversation.",
        )

    if deployment.status.value == "failed":
        raise HTTPException(
            status_code=402,
            detail={
                "message": deployment.error_message or "Échec du paiement",
                "deployment_id": deployment.id,
            },
        )

    if billing_event and billing_event.status.value == "pending":
        return DeployResponse(
            deployment=DeploymentResponse.from_deployment(deployment),
            billing_event=BillingEventResponse.from_event(billing_event),
            message="Paiement en attente — finalisez le checkout puis confirmez.",
            checkout_url=billing_event.checkout_url,
            payment_code=billing_event.payment_provider_charge_id,
            payment_pending=True,
        )

    return DeployResponse(
        deployment=DeploymentResponse.from_deployment(deployment),
        billing_event=BillingEventResponse.from_event(billing_event),
        message=f"Déploiement réussi — {deployment.deployment_cost:.2f} {deployment.currency} facturés.",
    )


@router.post("/{conversation_id}/deploy/confirm", response_model=DeployResponse)
async def confirm_deployment_payment(
    conversation_id: str,
    body: ConfirmPaymentRequest,
    ctx: UserContext = Depends(get_current_user),
    deployment_service: DeploymentService = Depends(get_deployment_service),
) -> DeployResponse:
    try:
        deployment, billing_event = await deployment_service.confirm_deployment_payment(
            conversation_id, ctx.organization.id, body.payment_code
        )
    except ValueError as exc:
        raise HTTPException(status_code=400, detail=str(exc)) from exc

    if billing_event.status.value != "succeeded":
        return DeployResponse(
            deployment=DeploymentResponse.from_deployment(deployment),
            billing_event=BillingEventResponse.from_event(billing_event),
            message=billing_event.description or "Paiement toujours en attente.",
            payment_code=body.payment_code,
            payment_pending=True,
        )

    return DeployResponse(
        deployment=DeploymentResponse.from_deployment(deployment),
        billing_event=BillingEventResponse.from_event(billing_event),
        message=f"Déploiement confirmé — {deployment.deployment_cost:.2f} {deployment.currency} facturés.",
    )

from fastapi import APIRouter, Depends, HTTPException, status
from sqlalchemy.ext.asyncio import AsyncSession

from agent_creator.dependencies import (
    UserContext,
    get_auth_service,
    get_current_user,
    get_db,
    get_db_store,
    get_deployment_service,
)
from agent_creator.schemas_auth import AuthMeResponse, LoginRequest, RegisterRequest, TokenResponse, UserResponse
from agent_creator.schemas_billing import OrganizationResponse
from agent_creator.services.auth import AuthError, AuthService
from agent_creator.services.deployment import DeploymentService
from sqlalchemy.ext.asyncio import AsyncSession

router = APIRouter(prefix="/auth", tags=["authentification"])


@router.post("/register", response_model=TokenResponse, status_code=status.HTTP_201_CREATED)
async def register(
    body: RegisterRequest,
    session: AsyncSession = Depends(get_db),
    auth: AuthService = Depends(get_auth_service),
) -> TokenResponse:
    """Crée un compte, une organisation (plan Free) et retourne un JWT."""
    try:
        _user, _org, token = await auth.register(
            session,
            email=body.email,
            password=body.password,
            full_name=body.full_name,
            organization_name=body.organization_name,
        )
    except AuthError as exc:
        raise HTTPException(status_code=status.HTTP_400_BAD_REQUEST, detail=str(exc)) from exc
    return TokenResponse(access_token=token)


@router.post("/login", response_model=TokenResponse)
async def login(
    body: LoginRequest,
    session: AsyncSession = Depends(get_db),
    auth: AuthService = Depends(get_auth_service),
) -> TokenResponse:
    try:
        _user, _org, token = await auth.login(session, email=body.email, password=body.password)
    except AuthError as exc:
        raise HTTPException(status_code=status.HTTP_401_UNAUTHORIZED, detail=str(exc)) from exc
    return TokenResponse(access_token=token)


@router.get("/me", response_model=AuthMeResponse)
async def me(
    ctx: UserContext = Depends(get_current_user),
    deployment_service: DeploymentService = Depends(get_deployment_service),
) -> AuthMeResponse:
    summary = await deployment_service.get_billing_summary(ctx.organization.id)
    plan_config = summary["plan_config"]
    return AuthMeResponse(
        user=UserResponse(id=ctx.user.id, email=ctx.user.email, full_name=ctx.user.full_name),
        organization=OrganizationResponse.from_organization(ctx.organization),
        plan_name=plan_config.name,
        deployments_used_this_month=summary["deployments_used_this_month"],
        deployments_limit=summary["deployments_limit"],
        monthly_subscription_eur=plan_config.monthly_price_eur,
    )

from urllib.parse import quote

from fastapi import APIRouter, Depends, HTTPException, status
from fastapi.responses import RedirectResponse
from pydantic import BaseModel, EmailStr
from sqlalchemy.ext.asyncio import AsyncSession

from agent_creator.config import Settings, get_settings
from agent_creator.dependencies import (
    UserContext,
    get_auth_service,
    get_current_user,
    get_db,
    get_deployment_service,
    get_oauth_service,
)
from agent_creator.schemas_auth import (
    AuthMeResponse,
    LoginRequest,
    OAuthProvidersResponse,
    RegisterRequest,
    TokenResponse,
    UserResponse,
)
from agent_creator.schemas_billing import OrganizationResponse
from agent_creator.services.auth import AuthError, AuthService
from agent_creator.services.deployment import DeploymentService
from agent_creator.services.email_service import EmailService
from agent_creator.services.oauth import OAuthError, OAuthService, list_enabled_providers

router = APIRouter(prefix="/auth", tags=["authentification"])


@router.get("/oauth/providers", response_model=OAuthProvidersResponse)
async def oauth_providers(settings: Settings = Depends(get_settings)) -> OAuthProvidersResponse:
    return OAuthProvidersResponse(providers=list_enabled_providers(settings))


@router.get("/oauth/{provider}")
async def oauth_start(
    provider: str,
    oauth: OAuthService = Depends(get_oauth_service),
) -> RedirectResponse:
    try:
        url = oauth.build_authorize_url(provider)
    except OAuthError as exc:
        raise HTTPException(status_code=status.HTTP_503_SERVICE_UNAVAILABLE, detail=str(exc)) from exc
    return RedirectResponse(url=url, status_code=status.HTTP_302_FOUND)


@router.get("/oauth/{provider}/callback")
async def oauth_callback(
    provider: str,
    code: str | None = None,
    state: str | None = None,
    error: str | None = None,
    error_description: str | None = None,
    session: AsyncSession = Depends(get_db),
    auth: AuthService = Depends(get_auth_service),
    oauth: OAuthService = Depends(get_oauth_service),
    settings: Settings = Depends(get_settings),
) -> RedirectResponse:
    base = settings.oauth_redirect_base_url.rstrip("/")
    fail_url = f"{base}/connexion/oauth#error={quote('Connexion annulée')}"

    if error:
        message = error_description or error
        return RedirectResponse(url=f"{base}/connexion/oauth#error={quote(message)}", status_code=302)
    if not code or not state:
        return RedirectResponse(url=fail_url, status_code=302)

    try:
        profile = await oauth.complete_login(provider, code, state)
        _user, _org, token = await auth.oauth_login_or_register(session, profile)
    except (OAuthError, AuthError) as exc:
        return RedirectResponse(
            url=f"{base}/connexion/oauth#error={quote(str(exc))}",
            status_code=302,
        )

    return RedirectResponse(url=f"{base}/connexion/oauth#token={quote(token)}", status_code=302)


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


# ─── Password Reset ───────────────────────────────────────────────────────────

class ForgotPasswordRequest(BaseModel):
    email: EmailStr


class ForgotPasswordResponse(BaseModel):
    message: str


class ResetPasswordRequest(BaseModel):
    token: str
    new_password: str


@router.post("/forgot-password", response_model=ForgotPasswordResponse)
async def forgot_password(
    body: ForgotPasswordRequest,
    session: AsyncSession = Depends(get_db),
    auth: AuthService = Depends(get_auth_service),
    settings: Settings = Depends(get_settings),
) -> ForgotPasswordResponse:
    """Demande de réinitialisation du mot de passe.
    Envoie un email avec le lien si l'adresse existe.
    Répond toujours avec le même message (anti-énumération).
    """
    generic_response = ForgotPasswordResponse(
        message="Si un compte existe pour cette adresse, un email de réinitialisation a été envoyé."
    )

    result = await auth.create_password_reset_token(session, email=body.email)
    if not result:
        return generic_response

    raw_token, full_name = result
    reset_url = f"{settings.app_base_url.rstrip('/')}/connexion?reset_token={raw_token}"

    email_svc = EmailService(settings)
    if email_svc.is_configured:
        await email_svc.send_password_reset(
            to_email=str(body.email),
            full_name=full_name,
            reset_link=reset_url,
        )
    else:
        import logging
        logging.getLogger("auth").warning(
            "[EMAIL SKIPPED] Reset password pour %s — GiseMailSender non configuré. Lien : %s",
            body.email,
            reset_url,
        )

    return generic_response


@router.post("/reset-password", response_model=ForgotPasswordResponse)
async def reset_password(
    body: ResetPasswordRequest,
    session: AsyncSession = Depends(get_db),
    auth: AuthService = Depends(get_auth_service),
) -> ForgotPasswordResponse:
    """Réinitialise le mot de passe avec le token reçu par email."""
    try:
        await auth.reset_password(session, token=body.token, new_password=body.new_password)
    except AuthError as exc:
        raise HTTPException(status_code=status.HTTP_400_BAD_REQUEST, detail=str(exc)) from exc
    return ForgotPasswordResponse(message="Votre mot de passe a été réinitialisé. Vous pouvez vous connecter.")


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

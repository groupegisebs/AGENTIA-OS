from pydantic import BaseModel, EmailStr, Field

from agent_creator.schemas_billing import OrganizationResponse


class RegisterRequest(BaseModel):
    email: EmailStr
    password: str = Field(min_length=8, description="Minimum 8 caractères")
    full_name: str = Field(min_length=2)
    organization_name: str = Field(min_length=2)


class LoginRequest(BaseModel):
    email: EmailStr
    password: str


class TokenResponse(BaseModel):
    access_token: str
    token_type: str = "bearer"


class UserResponse(BaseModel):
    id: str
    email: str
    full_name: str


class AuthMeResponse(BaseModel):
    user: UserResponse
    organization: OrganizationResponse
    plan_name: str
    deployments_used_this_month: int
    deployments_limit: int | None
    monthly_subscription_eur: float


class ConfirmBillingRequest(BaseModel):
    payment_code: str
    intent_type: str | None = None

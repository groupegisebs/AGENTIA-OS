from datetime import datetime, timedelta, timezone
import secrets
from uuid import uuid4

from jose import JWTError, jwt
from passlib.context import CryptContext
from sqlalchemy import select
from sqlalchemy.ext.asyncio import AsyncSession

from agent_creator.config import Settings
from agent_creator.db.tables import MembershipRow, OAuthIdentityRow, OrganizationRow, UserRow
from agent_creator.models.organization import Organization
from agent_creator.models.subscription import SubscriptionPlan
from agent_creator.models.user import Membership, MembershipRole, User
from agent_creator.services.oauth import OAuthProfile

pwd_context = CryptContext(schemes=["bcrypt"], deprecated="auto")


class AuthError(Exception):
    pass


class AuthService:
    def __init__(self, settings: Settings) -> None:
        self._settings = settings

    def hash_password(self, password: str) -> str:
        return pwd_context.hash(password)

    def verify_password(self, plain: str, hashed: str) -> bool:
        return pwd_context.verify(plain, hashed)

    def create_access_token(self, user_id: str, organization_id: str) -> str:
        expire = datetime.now(timezone.utc) + timedelta(minutes=self._settings.jwt_expire_minutes)
        payload = {
            "sub": user_id,
            "org_id": organization_id,
            "exp": expire,
        }
        return jwt.encode(payload, self._settings.jwt_secret, algorithm=self._settings.jwt_algorithm)

    def decode_token(self, token: str) -> tuple[str, str]:
        try:
            payload = jwt.decode(token, self._settings.jwt_secret, algorithms=[self._settings.jwt_algorithm])
            user_id = payload.get("sub")
            org_id = payload.get("org_id")
            if not user_id or not org_id:
                raise AuthError("Token invalide")
            return user_id, org_id
        except JWTError as exc:
            raise AuthError("Token invalide ou expiré") from exc

    async def register(
        self,
        session: AsyncSession,
        *,
        email: str,
        password: str,
        full_name: str,
        organization_name: str,
    ) -> tuple[User, Organization, str]:
        existing = await session.scalar(select(UserRow).where(UserRow.email == email.lower()))
        if existing:
            raise AuthError("Un compte existe déjà avec cet email")

        user_id = str(uuid4())
        org_id = str(uuid4())
        membership_id = str(uuid4())

        user_row = UserRow(
            id=user_id,
            email=email.lower(),
            password_hash=self.hash_password(password),
            full_name=full_name,
        )
        org_row = OrganizationRow(
            id=org_id,
            name=organization_name,
            plan=SubscriptionPlan.FREE.value,
            billing_email=email.lower(),
            gisebs_customer_code=f"AF-{org_id[:8]}",
        )
        membership_row = MembershipRow(
            id=membership_id,
            user_id=user_id,
            organization_id=org_id,
            role=MembershipRole.OWNER.value,
        )

        session.add_all([user_row, org_row, membership_row])
        await session.flush()

        user = User(id=user_id, email=email.lower(), full_name=full_name)
        org = Organization(
            id=org_id,
            name=organization_name,
            plan=SubscriptionPlan.FREE,
            billing_email=email.lower(),
            stripe_customer_id=f"AF-{org_id[:8]}",
        )
        token = self.create_access_token(user_id, org_id)
        return user, org, token

    async def login(self, session: AsyncSession, *, email: str, password: str) -> tuple[User, Organization, str]:
        user_row = await session.scalar(select(UserRow).where(UserRow.email == email.lower()))
        if not user_row or not user_row.password_hash or not self.verify_password(password, user_row.password_hash):
            raise AuthError("Email ou mot de passe incorrect")

        membership = await session.scalar(
            select(MembershipRow)
            .where(MembershipRow.user_id == user_row.id)
            .order_by(MembershipRow.created_at)
        )
        if not membership:
            raise AuthError("Aucune organisation associée à ce compte")

        org_row = await session.get(OrganizationRow, membership.organization_id)
        if not org_row:
            raise AuthError("Organisation introuvable")

        user = User(id=user_row.id, email=user_row.email, full_name=user_row.full_name, created_at=user_row.created_at)
        org = _org_from_row(org_row)
        token = self.create_access_token(user_row.id, org_row.id)
        return user, org, token

    async def get_user_context(
        self, session: AsyncSession, user_id: str, organization_id: str
    ) -> tuple[User, Organization, Membership]:
        user_row = await session.get(UserRow, user_id)
        org_row = await session.get(OrganizationRow, organization_id)
        if not user_row or not org_row:
            raise AuthError("Session invalide")

        membership = await session.scalar(
            select(MembershipRow).where(
                MembershipRow.user_id == user_id,
                MembershipRow.organization_id == organization_id,
            )
        )
        if not membership:
            raise AuthError("Accès refusé à cette organisation")

        user = User(id=user_row.id, email=user_row.email, full_name=user_row.full_name, created_at=user_row.created_at)
        org = _org_from_row(org_row)
        mem = Membership(
            id=membership.id,
            user_id=membership.user_id,
            organization_id=membership.organization_id,
            role=MembershipRole(membership.role),
            created_at=membership.created_at,
        )
        return user, org, mem

    async def oauth_login_or_register(
        self,
        session: AsyncSession,
        profile: OAuthProfile,
    ) -> tuple[User, Organization, str]:
        identity = await session.scalar(
            select(OAuthIdentityRow).where(
                OAuthIdentityRow.provider == profile.provider,
                OAuthIdentityRow.provider_user_id == profile.provider_user_id,
            )
        )
        if identity:
            return await self._login_existing_user(session, identity.user_id)

        user_row = await session.scalar(select(UserRow).where(UserRow.email == profile.email.lower()))
        if user_row:
            session.add(
                OAuthIdentityRow(
                    id=str(uuid4()),
                    user_id=user_row.id,
                    provider=profile.provider,
                    provider_user_id=profile.provider_user_id,
                )
            )
            await session.flush()
            return await self._login_existing_user(session, user_row.id)

        user_id = str(uuid4())
        org_id = str(uuid4())
        membership_id = str(uuid4())
        identity_id = str(uuid4())
        org_name = _default_org_name(profile.full_name, profile.email)

        user_row = UserRow(
            id=user_id,
            email=profile.email.lower(),
            password_hash=self.hash_password(secrets.token_urlsafe(32)),
            full_name=profile.full_name,
        )
        org_row = OrganizationRow(
            id=org_id,
            name=org_name,
            plan=SubscriptionPlan.FREE.value,
            billing_email=profile.email.lower(),
            gisebs_customer_code=f"AF-{org_id[:8]}",
        )
        membership_row = MembershipRow(
            id=membership_id,
            user_id=user_id,
            organization_id=org_id,
            role=MembershipRole.OWNER.value,
        )
        identity_row = OAuthIdentityRow(
            id=identity_id,
            user_id=user_id,
            provider=profile.provider,
            provider_user_id=profile.provider_user_id,
        )
        session.add_all([user_row, org_row, membership_row, identity_row])
        await session.flush()

        user = User(id=user_id, email=profile.email.lower(), full_name=profile.full_name)
        org = Organization(
            id=org_id,
            name=org_name,
            plan=SubscriptionPlan.FREE,
            billing_email=profile.email.lower(),
            stripe_customer_id=f"AF-{org_id[:8]}",
        )
        token = self.create_access_token(user_id, org_id)
        return user, org, token

    async def _login_existing_user(
        self, session: AsyncSession, user_id: str
    ) -> tuple[User, Organization, str]:
        user_row = await session.get(UserRow, user_id)
        if not user_row:
            raise AuthError("Compte introuvable")

        membership = await session.scalar(
            select(MembershipRow)
            .where(MembershipRow.user_id == user_row.id)
            .order_by(MembershipRow.created_at)
        )
        if not membership:
            raise AuthError("Aucune organisation associée à ce compte")

        org_row = await session.get(OrganizationRow, membership.organization_id)
        if not org_row:
            raise AuthError("Organisation introuvable")

        user = User(id=user_row.id, email=user_row.email, full_name=user_row.full_name, created_at=user_row.created_at)
        org = _org_from_row(org_row)
        token = self.create_access_token(user_row.id, org_row.id)
        return user, org, token


def _default_org_name(full_name: str, email: str) -> str:
    name = full_name.strip()
    if name:
        return f"Organisation de {name}"
    local = email.split("@", 1)[0]
    return f"Organisation {local}"


def _org_from_row(row: OrganizationRow) -> Organization:
    return Organization(
        id=row.id,
        name=row.name,
        plan=SubscriptionPlan(row.plan),
        billing_email=row.billing_email,
        stripe_customer_id=row.gisebs_customer_code,
        created_at=row.created_at,
        updated_at=row.updated_at,
    )

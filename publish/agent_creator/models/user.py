from datetime import datetime
from enum import Enum
from uuid import uuid4

from pydantic import BaseModel, EmailStr, Field


class MembershipRole(str, Enum):
    OWNER = "owner"
    ADMIN = "admin"
    MEMBER = "member"


class User(BaseModel):
    id: str = Field(default_factory=lambda: str(uuid4()))
    email: EmailStr
    full_name: str
    created_at: datetime = Field(default_factory=datetime.utcnow)


class Membership(BaseModel):
    id: str = Field(default_factory=lambda: str(uuid4()))
    user_id: str
    organization_id: str
    role: MembershipRole = MembershipRole.OWNER
    created_at: datetime = Field(default_factory=datetime.utcnow)

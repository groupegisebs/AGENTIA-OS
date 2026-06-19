from datetime import datetime
from enum import Enum
from uuid import uuid4

from pydantic import BaseModel, Field


class MessageRole(str, Enum):
    USER = "user"
    ASSISTANT = "assistant"
    SYSTEM = "system"


class Message(BaseModel):
    id: str = Field(default_factory=lambda: str(uuid4()))
    role: MessageRole
    content: str
    created_at: datetime = Field(default_factory=datetime.utcnow)


class ConversationStatus(str, Enum):
    ACTIVE = "active"
    READY_FOR_BLUEPRINT = "ready_for_blueprint"
    BLUEPRINT_GENERATED = "blueprint_generated"


class Conversation(BaseModel):
    id: str = Field(default_factory=lambda: str(uuid4()))
    title: str | None = None
    status: ConversationStatus = ConversationStatus.ACTIVE
    messages: list[Message] = Field(default_factory=list)
    clarifying_questions: list[str] = Field(default_factory=list)
    created_at: datetime = Field(default_factory=datetime.utcnow)
    updated_at: datetime = Field(default_factory=datetime.utcnow)

    def add_message(self, role: MessageRole, content: str) -> Message:
        message = Message(role=role, content=content)
        self.messages.append(message)
        self.updated_at = datetime.utcnow()
        if role == MessageRole.USER and not self.title:
            self.title = content[:80] + ("..." if len(content) > 80 else "")
        return message

    @property
    def user_messages(self) -> list[str]:
        return [m.content for m in self.messages if m.role == MessageRole.USER]

    @property
    def transcript(self) -> str:
        lines: list[str] = []
        for msg in self.messages:
            label = {"user": "Utilisateur", "assistant": "Agent", "system": "Système"}[msg.role.value]
            lines.append(f"{label}: {msg.content}")
        return "\n".join(lines)

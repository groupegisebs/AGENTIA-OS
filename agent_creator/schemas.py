from datetime import datetime

from pydantic import BaseModel, Field

from agent_creator.models.blueprint import Blueprint
from agent_creator.models.conversation import Conversation, ConversationStatus, MessageRole


class CreateConversationRequest(BaseModel):
    message: str = Field(..., min_length=1, description="Premier message décrivant le besoin métier")


class SendMessageRequest(BaseModel):
    message: str = Field(..., min_length=1, description="Message de l'utilisateur")


class MessageResponse(BaseModel):
    id: str
    role: MessageRole
    content: str
    created_at: datetime


class ConversationResponse(BaseModel):
    id: str
    title: str | None
    status: ConversationStatus
    messages: list[MessageResponse]
    clarifying_questions: list[str]
    created_at: datetime
    updated_at: datetime
    llm_mode: str

    @classmethod
    def from_conversation(cls, conversation: Conversation, llm_mode: str) -> "ConversationResponse":
        return cls(
            id=conversation.id,
            title=conversation.title,
            status=conversation.status,
            messages=[
                MessageResponse(
                    id=m.id,
                    role=m.role,
                    content=m.content,
                    created_at=m.created_at,
                )
                for m in conversation.messages
            ],
            clarifying_questions=conversation.clarifying_questions,
            created_at=conversation.created_at,
            updated_at=conversation.updated_at,
            llm_mode=llm_mode,
        )


class AssistantReplyResponse(BaseModel):
    conversation: ConversationResponse
    assistant_message: MessageResponse


class BlueprintResponse(BaseModel):
    blueprint: Blueprint
    llm_mode: str
    deployment_hint: str | None = Field(
        default=None,
        description="Estimation du coût de déploiement (action facturable)",
    )

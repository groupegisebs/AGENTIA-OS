from agent_creator.models.blueprint import Blueprint
from agent_creator.models.conversation import Conversation


class ConversationStore:
    """Stockage en mémoire pour le MVP."""

    def __init__(self) -> None:
        self._conversations: dict[str, Conversation] = {}
        self._blueprints: dict[str, Blueprint] = {}

    def create(self, conversation: Conversation) -> Conversation:
        self._conversations[conversation.id] = conversation
        return conversation

    def get(self, conversation_id: str) -> Conversation | None:
        return self._conversations.get(conversation_id)

    def save(self, conversation: Conversation) -> Conversation:
        self._conversations[conversation.id] = conversation
        return conversation

    def list_all(self) -> list[Conversation]:
        return list(self._conversations.values())

    def save_blueprint(self, blueprint: Blueprint) -> Blueprint:
        self._blueprints[blueprint.conversation_id] = blueprint
        return blueprint

    def get_blueprint(self, conversation_id: str) -> Blueprint | None:
        return self._blueprints.get(conversation_id)

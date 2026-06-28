namespace AgentiaOs.Domain.Entities;

public sealed class Conversation
{
    public Guid Id { get; set; }
    public Guid OwnerUserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public List<ConversationMessage> Messages { get; set; } = [];
}

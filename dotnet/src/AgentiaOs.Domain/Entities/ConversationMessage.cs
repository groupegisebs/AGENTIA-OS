using System.Text.Json.Serialization;

namespace AgentiaOs.Domain.Entities;

public sealed class ConversationMessage
{
    public Guid Id { get; set; }
    public Guid ConversationId { get; set; }
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    [JsonIgnore]
    public Conversation? Conversation { get; set; }
}

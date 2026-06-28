namespace AgentiaOs.Domain.Entities;

public sealed class DeploymentJob
{
    public Guid Id { get; set; }
    public Guid ConversationId { get; set; }
    public string TargetEnvironment { get; set; } = "dev";
    public string Status { get; set; } = "pending";
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
}

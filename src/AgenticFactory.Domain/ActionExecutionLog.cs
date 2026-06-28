namespace AgenticFactory.Domain;

/// <summary>
/// Future monitoring model for per-action execution telemetry.
/// Phase 1 stub — wire into monitoring UI in a later phase.
/// </summary>
public sealed class ActionExecutionLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RunId { get; set; }
    public Guid? AgentActionId { get; set; }
    public Guid? ExecutionProviderId { get; set; }
    public string ProviderType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int DurationMs { get; set; }
    public string? ErrorMessage { get; set; }
    public int RetryCount { get; set; }
    public DateTime StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
}

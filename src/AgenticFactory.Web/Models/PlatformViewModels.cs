namespace AgenticFactory.Web.Models;

public sealed class AgentsIndexViewModel
{
    public AgentsSummary Summary { get; set; } = new(0, 0, 0, 0, 0);
    public List<AgentListItem> Agents { get; set; } = [];
    public string UserDisplayName { get; set; } = "Utilisateur";
    public string UserRole { get; set; } = "Membre";
    public int[] WeeklyActivity { get; set; } = [0, 0, 0, 0, 0, 0, 0];
}

public sealed record AgentsSummary(int Total, int Active, int Running, int Paused, int Disabled);

public sealed record AgentListItem(
    Guid Id,
    string Name,
    string Description,
    string EndpointSlug,
    string Status,
    string DisplayStatus,
    string Category,
    DateTime CreatedAtUtc,
    Guid? LatestBlueprintId,
    string VersionLabel,
    string Environment,
    DateTime? LastRunAt,
    int RunsLast7Days,
    int RunsLast30Days,
    decimal CostLast30Days,
    int[] RunsSparkline);

public sealed class CreateAgentViewModel
{
    public string Message { get; set; } = string.Empty;
    public string? WizardJson { get; set; }
}

public sealed class StudioDomainRequestModel
{
    public string DomainName { get; set; } = string.Empty;
    public string? Industry { get; set; }
    public string? UseCase { get; set; }
    public string? Description { get; set; }
}

public sealed class StudioObjectiveRequestModel
{
    public string ObjectiveName { get; set; } = string.Empty;
    public string? RelatedDomain { get; set; }
    public string? UseCase { get; set; }
    public string? Description { get; set; }
}

public sealed class StudioEstimateRequestModel
{
    public bool HasDomain { get; set; }
    public int ObjectiveCount { get; set; }
    public int SourceCount { get; set; }
    public int ActionCount { get; set; }
    public int AutonomyLevel { get; set; }
    public string? TriggerId { get; set; }
    public string? TriggerFrequency { get; set; }
    public string? RuntimeId { get; set; }
    public bool HeartbeatEnabled { get; set; }
}

public sealed class RecommendExecutionProviderRequestModel
{
    public string? ActionId { get; set; }
    public string? ActuatorType { get; set; }
    public List<string>? Sensors { get; set; }
    public List<string>? Tools { get; set; }
}

public sealed class DeploymentsIndexViewModel
{
    public List<DeploymentAgentGroup> AgentGroups { get; set; } = [];
}

public sealed record DeploymentAgentGroup(
    Guid AgentId,
    string AgentName,
    string EndpointSlug,
    string CurrentVersion,
    string Environment,
    string Status,
    DateTime? LastDeployedAt,
    int DeploymentCount);

public sealed class DeploymentDetailViewModel
{
    public Guid AgentId { get; set; }
    public string AgentName { get; set; } = "";
    public string Description { get; set; } = "";
    public string EndpointSlug { get; set; } = "";
    public string InvokeUrl { get; set; } = "";
    public string VersionLabel { get; set; } = "—";
    public string AgentStatus { get; set; } = "";
    public Guid? LatestBlueprintId { get; set; }
    public List<PipelineStageItem> Pipeline { get; set; } = [];
    public List<VersionRowItem> Versions { get; set; } = [];
    public ProductionDetailItem? Production { get; set; }
    public UsageDetailItem Usage { get; set; } = new(0, 0, 0, 0, []);
    public List<TimelineItem> RecentTimeline { get; set; } = [];
    public List<OperationLogItem> OperationLogs { get; set; } = [];
    public TestInvokeFormModel TestInvoke { get; set; } = new();
    public InvokeTestResultViewModel? InvokeResult { get; set; }
}

public sealed class TestInvokeFormModel
{
    public string ApiKey { get; set; } = "";
    public string InputJson { get; set; } = """{"message": "Test invoke"}""";
}

public sealed class InvokeTestResultViewModel
{
    public bool Success { get; set; }
    public int? HttpStatus { get; set; }
    public string? ErrorMessage { get; set; }
    public Guid? RunId { get; set; }
    public string? Status { get; set; }
    public string? OutputJson { get; set; }
    public int? PromptTokens { get; set; }
    public int? CompletionTokens { get; set; }
    public decimal? EstimatedCostUsd { get; set; }
}

public sealed record PipelineStageItem(string Stage, string Label, string Status, DateTime? DeployedAt, string? VersionLabel);
public sealed record VersionRowItem(Guid Id, string Label, string Description, DateTime CreatedAt, string CreatedBy, bool IsCurrent, string Status);
public sealed record ProductionDetailItem(string Environment, string Status, DateTime? ActivatedAt, string ApiKeyMasked, string RuntimeNode, string RuntimeStatus, int UptimeDays, int UptimeHours, List<string> TriggerTypes);
public sealed record UsageDetailItem(int Runs, long Tokens, decimal Cost, int Errors, List<int> TokenSeries);
public sealed record TimelineItem(string Environment, string VersionLabel, DateTime At, string Outcome);
public sealed record OperationLogItem(DateTime At, string Level, string Message);

public sealed record DeploymentListItem(
    Guid Id,
    Guid AgentId,
    string AgentName,
    string EndpointSlug,
    int VersionNumber,
    string Status,
    string Environment,
    DateTime? ActivatedAtUtc,
    DateTime CreatedAtUtc);

public sealed class ExecutionsIndexViewModel
{
    public List<RunListItem> Runs { get; set; } = [];
}

public sealed record RunListItem(
    Guid Id,
    Guid AgentId,
    string AgentName,
    string Status,
    DateTime CreatedAtUtc,
    decimal EstimatedCostUsd,
    int PromptTokens,
    int CompletionTokens,
    string ErrorMessage);

public sealed class ExecutionProvidersIndexViewModel
{
    public List<ExecutionProviderListItem> Providers { get; set; } = [];
}

public sealed record ExecutionProviderListItem(
    Guid Id,
    string Name,
    string Description,
    string Category,
    string ProviderType,
    string Version,
    string Author,
    string State,
    bool IsEnabled,
    bool SupportsMonitoring,
    bool SupportsRetry,
    bool SupportsRollback,
    bool SupportsScheduling,
    bool SupportsParameters);

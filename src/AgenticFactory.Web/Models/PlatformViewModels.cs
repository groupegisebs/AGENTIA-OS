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

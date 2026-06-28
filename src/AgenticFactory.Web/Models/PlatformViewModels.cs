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
    [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Décrivez ce que l'agent doit faire.")]
    [System.ComponentModel.DataAnnotations.MinLength(10, ErrorMessage = "Minimum 10 caractères.")]
    public string Message { get; set; } = string.Empty;
}

public sealed class DeploymentsIndexViewModel
{
    public List<DeploymentListItem> Deployments { get; set; } = [];
}

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

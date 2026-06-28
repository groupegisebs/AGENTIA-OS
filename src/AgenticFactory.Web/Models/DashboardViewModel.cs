namespace AgenticFactory.Web.Models;

public sealed class DashboardViewModel
{
    public DashboardStatsDto Stats { get; set; } = new(0, 0, 0, 0, 0, 0, 0, 0, 0);
    public int ActiveAgents { get; set; }
    public List<RunItem> RecentRuns { get; set; } = [];
    public List<RuntimeStatusDto> RuntimeStatuses { get; set; } = [];
    public List<DailyRunChartPoint> DailyRuns { get; set; } = [];
    public List<StatusBreakdownItem> StatusBreakdown { get; set; } = [];
    public List<int> TokenSeries { get; set; } = [];
    public string UserDisplayName { get; set; } = "Utilisateur";
    public string UserRole { get; set; } = "Membre";
}

public sealed record DashboardStatsDto(
    int TotalAgents, int TotalRuns, int TotalErrors,
    long TotalTokens, double TotalCostUsd,
    int TodayRuns, int TodayErrors, long TodayTokens, double TodayCostUsd);

public sealed record RunItem(
    Guid Id,
    string AgentName,
    string Status,
    DateTime CreatedAt,
    double? DurationSeconds,
    double CostUsd,
    int PromptTokens,
    int CompletionTokens);

public sealed record RuntimeStatusDto(
    string NodeName,
    string Status,
    DateTime LastSeen,
    int ActiveTriggers);

public sealed record DailyRunChartPoint(
    string Label,
    int Success,
    int Failed,
    int Running,
    int Queued);

public sealed record StatusBreakdownItem(string Status, int Count);

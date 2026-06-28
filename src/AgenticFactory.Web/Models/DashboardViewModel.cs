namespace AgenticFactory.Web.Models;

public sealed class DashboardViewModel
{
    public DashboardStatsDto Stats { get; set; } = new(0, 0, 0, 0, 0, 0, 0, 0, 0);
    public List<RunItem> RecentRuns { get; set; } = [];
    public List<RuntimeStatusDto> RuntimeStatuses { get; set; } = [];
}

public sealed record DashboardStatsDto(
    int TotalAgents, int TotalRuns, int TotalErrors,
    long TotalTokens, double TotalCostUsd,
    int TodayRuns, int TodayErrors, long TodayTokens, double TodayCostUsd);

public sealed record RunItem(
    Guid Id, string Status, DateTime CreatedAt,
    double CostUsd, int PromptTokens, int CompletionTokens);

public sealed record RuntimeStatusDto(
    string NodeName, string Status, DateTime LastSeen, int ActiveTriggers);

using AgenticFactory.Shared;

namespace AgenticFactory.Web.Models;

public sealed class DashboardViewModel
{
    public required DashboardStatsDto Stats { get; set; }
    public required List<RunItem> RecentRuns { get; set; }
    public required List<RuntimeStatusDto> RuntimeStatuses { get; set; }
}

public sealed record RunItem(Guid Id, string Status, DateTime CreatedAtUtc, decimal EstimatedCostUsd, int PromptTokens, int CompletionTokens);

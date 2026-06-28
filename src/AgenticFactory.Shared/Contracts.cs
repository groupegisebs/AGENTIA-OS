namespace AgenticFactory.Shared;

public static class SystemRoles
{
    public const string Admin = "Admin";
    public const string Creator = "Creator";
    public const string Viewer = "Viewer";
}

public sealed record RuntimeStatusDto(string NodeName, string Status, DateTime LastSeenUtc, int ActiveTriggerCount);

public sealed record DashboardStatsDto(
    int TotalAgents,
    int TotalRuns,
    int FailedRuns,
    int TotalTokens,
    decimal EstimatedCostUsd);

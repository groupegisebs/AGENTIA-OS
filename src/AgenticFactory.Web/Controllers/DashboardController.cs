using System.Security.Claims;
using AgenticFactory.Web.Models;
using AgenticFactory.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgenticFactory.Web.Controllers;

[Authorize]
public class DashboardController(ApiClient api) : AuthenticatedController
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        SetActiveNav("Dashboard");
        ViewData["DashboardMode"] = true;
        AuthenticateApi(api);

        var dashboard = await api.GetDashboardAsync();
        var vm = new DashboardViewModel
        {
            UserDisplayName = User.FindFirstValue(ClaimTypes.Name) ?? User.Identity?.Name ?? "Utilisateur",
            UserRole = User.FindFirstValue(ClaimTypes.Role) ?? "Membre"
        };

        if (dashboard is not null)
        {
            vm.Stats = new DashboardStatsDto(
                dashboard.Stats.TotalAgents,
                dashboard.Stats.TotalRuns,
                dashboard.Stats.FailedRuns,
                dashboard.Stats.TotalTokens,
                (double)dashboard.Stats.EstimatedCostUsd,
                dashboard.Stats.RunsToday,
                dashboard.Stats.FailedRunsToday,
                dashboard.Stats.TokensToday,
                (double)dashboard.Stats.EstimatedCostTodayUsd);
            vm.ActiveAgents = dashboard.ActiveAgents;
            vm.RecentRuns = (dashboard.RecentRuns ?? [])
                .Select(MapRun)
                .ToList();
            vm.RuntimeStatuses = (dashboard.RuntimeStatuses ?? [])
                .Select(r => new RuntimeStatusDto(r.NodeName, r.Status, r.LastSeenUtc, r.ActiveTriggerCount))
                .ToList();
            vm.DailyRuns = (dashboard.DailyRuns ?? [])
                .Select(d => new DailyRunChartPoint(d.Label, d.Success, d.Failed, d.Running, d.Queued))
                .ToList();
            vm.StatusBreakdown = (dashboard.StatusBreakdown ?? [])
                .Select(s => new StatusBreakdownItem(TranslateStatus(s.Status), s.Count))
                .ToList();
            vm.TokenSeries = dashboard.TokenSeries ?? [];
        }

        return View(vm);
    }

    private static RunItem MapRun(RunItemResponse r)
    {
        var duration = r.StartedAtUtc.HasValue && r.CompletedAtUtc.HasValue
            ? (r.CompletedAtUtc.Value - r.StartedAtUtc.Value).TotalSeconds
            : (double?)null;

        return new RunItem(
            r.Id,
            r.AgentName ?? "Agent",
            TranslateStatus(r.Status),
            r.CreatedAtUtc,
            duration,
            (double)r.EstimatedCostUsd,
            r.PromptTokens,
            r.CompletionTokens);
    }

    private static string TranslateStatus(int status) => status switch
    {
        1 => "En file",
        2 => "En cours",
        3 => "Succès",
        4 => "Erreur",
        _ => status.ToString()
    };

    private static string TranslateStatus(string status) => status switch
    {
        "Completed" => "Succès",
        "Failed" => "Erreur",
        "Running" => "En cours",
        "Queued" => "En file",
        _ => status
    };
}

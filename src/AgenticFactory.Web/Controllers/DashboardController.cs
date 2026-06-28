using System.Security.Claims;
using AgenticFactory.Web.Models;
using AgenticFactory.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgenticFactory.Web.Controllers;

[Authorize]
public class DashboardController(ApiClient api) : Controller
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        // Injecter le JWT de l'utilisateur dans le client HTTP
        var token = User.FindFirstValue("ApiToken");
        if (!string.IsNullOrEmpty(token))
            api.SetBearerToken(token);

        var dashboard = await api.GetDashboardAsync();

        var vm = dashboard is null
            ? new DashboardViewModel()
            : new DashboardViewModel
            {
                Stats = new DashboardStatsDto(
                    dashboard.Stats.TotalAgents,
                    dashboard.Stats.TotalRuns,
                    dashboard.Stats.TotalErrors,
                    dashboard.Stats.TotalTokens,
                    dashboard.Stats.TotalCostUsd,
                    dashboard.Stats.TodayRuns,
                    dashboard.Stats.TodayErrors,
                    dashboard.Stats.TodayTokens,
                    dashboard.Stats.TodayCostUsd),
                RecentRuns = dashboard.RecentRuns
                    .Select(r => new RunItem(r.Id, r.Status, r.CreatedAt, r.CostUsd, r.PromptTokens, r.CompletionTokens))
                    .ToList(),
                RuntimeStatuses = dashboard.RuntimeStatuses
                    .Select(r => new RuntimeStatusDto(r.NodeName, r.Status, r.LastSeen, r.ActiveTriggers))
                    .ToList()
            };

        return View(vm);
    }
}

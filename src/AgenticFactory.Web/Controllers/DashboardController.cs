using AgenticFactory.Domain;
using AgenticFactory.Infrastructure.Persistence;
using AgenticFactory.Shared;
using AgenticFactory.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AgenticFactory.Web.Controllers;

[Authorize]
public class DashboardController(AgenticFactoryDbContext dbContext) : Controller
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var vm = new DashboardViewModel
        {
            Stats = new DashboardStatsDto(
                await dbContext.Agents.CountAsync(cancellationToken),
                await dbContext.AgentRuns.CountAsync(cancellationToken),
                await dbContext.AgentRuns.CountAsync(x => x.Status == RunStatus.Failed, cancellationToken),
                await dbContext.AgentRuns.SumAsync(x => x.PromptTokens + x.CompletionTokens, cancellationToken),
                await dbContext.AgentRuns.SumAsync(x => x.EstimatedCostUsd, cancellationToken)),
            RecentRuns = await dbContext.AgentRuns
                .OrderByDescending(x => x.CreatedAtUtc)
                .Take(10)
                .Select(x => new RunItem(x.Id, x.Status.ToString(), x.CreatedAtUtc, x.EstimatedCostUsd, x.PromptTokens, x.CompletionTokens))
                .ToListAsync(cancellationToken),
            RuntimeStatuses = await dbContext.RuntimeHeartbeats
                .OrderByDescending(x => x.LastSeenUtc)
                .Select(x => new RuntimeStatusDto(x.NodeName, x.Status, x.LastSeenUtc, x.ActiveTriggerCount))
                .ToListAsync(cancellationToken)
        };

        return View(vm);
    }
}

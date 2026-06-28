using AgenticFactory.Domain;
using AgenticFactory.Infrastructure.Identity;
using AgenticFactory.Infrastructure.Persistence;
using AgenticFactory.Shared;
using AgenticFactory.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AgenticFactory.Web.Controllers;

[Authorize]
public class DashboardController(
    AgenticFactoryDbContext dbContext,
    UserManager<AppIdentityUser> userManager) : Controller
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null || user.OrganizationId == Guid.Empty)
        {
            return Forbid();
        }

        var organizationId = user.OrganizationId;
        var startOfDayUtc = DateTime.UtcNow.Date;
        var runsQuery = dbContext.AgentRuns.Where(x => x.OrganizationId == organizationId);
        var runsTodayQuery = runsQuery.Where(x => x.CreatedAtUtc >= startOfDayUtc);

        var vm = new DashboardViewModel
        {
            Stats = new DashboardStatsDto(
                await dbContext.Agents.CountAsync(x => x.OrganizationId == organizationId, cancellationToken),
                await runsQuery.CountAsync(cancellationToken),
                await runsQuery.CountAsync(x => x.Status == RunStatus.Failed, cancellationToken),
                await runsQuery.SumAsync(x => x.PromptTokens + x.CompletionTokens, cancellationToken),
                await runsQuery.SumAsync(x => x.EstimatedCostUsd, cancellationToken),
                await runsTodayQuery.CountAsync(cancellationToken),
                await runsTodayQuery.CountAsync(x => x.Status == RunStatus.Failed, cancellationToken),
                await runsTodayQuery.SumAsync(x => x.PromptTokens + x.CompletionTokens, cancellationToken),
                await runsTodayQuery.SumAsync(x => x.EstimatedCostUsd, cancellationToken)),
            RecentRuns = await runsQuery
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

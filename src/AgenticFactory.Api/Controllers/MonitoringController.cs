using AgenticFactory.Infrastructure.Persistence;
using AgenticFactory.Shared;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AgenticFactory.Api.Controllers;

[ApiController]
[Route("api/monitoring")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "CanViewDashboard")]
public class MonitoringController(AgenticFactoryDbContext dbContext) : ControllerBase
{
    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard(CancellationToken cancellationToken)
    {
        var stats = new DashboardStatsDto(
            await dbContext.Agents.CountAsync(cancellationToken),
            await dbContext.AgentRuns.CountAsync(cancellationToken),
            await dbContext.AgentRuns.CountAsync(x => x.Status == Domain.RunStatus.Failed, cancellationToken),
            await dbContext.AgentRuns.SumAsync(x => x.PromptTokens + x.CompletionTokens, cancellationToken),
            await dbContext.AgentRuns.SumAsync(x => x.EstimatedCostUsd, cancellationToken));

        var recentRuns = await dbContext.AgentRuns
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(20)
            .Select(x => new { x.Id, x.Status, x.CreatedAtUtc, x.EstimatedCostUsd, x.PromptTokens, x.CompletionTokens })
            .ToListAsync(cancellationToken);

        var runtime = await dbContext.RuntimeHeartbeats
            .OrderByDescending(x => x.LastSeenUtc)
            .Select(x => new RuntimeStatusDto(x.NodeName, x.Status, x.LastSeenUtc, x.ActiveTriggerCount))
            .ToListAsync(cancellationToken);

        return Ok(new { stats, recentRuns, runtime });
    }
}

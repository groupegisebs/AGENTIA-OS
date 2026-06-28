using AgenticFactory.Application;
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
public class MonitoringController(
    AgenticFactoryDbContext dbContext,
    ICurrentTenantService tenantService) : ControllerBase
{
    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard(CancellationToken cancellationToken)
    {
        var organizationId = tenantService.OrganizationId;
        if (organizationId == Guid.Empty)
        {
            return BadRequest("Missing organization context.");
        }

        var startOfDayUtc = DateTime.UtcNow.Date;
        var runsQuery = dbContext.AgentRuns.Where(x => x.OrganizationId == organizationId);
        var runsTodayQuery = runsQuery.Where(x => x.CreatedAtUtc >= startOfDayUtc);

        var stats = new DashboardStatsDto(
            await dbContext.Agents.CountAsync(x => x.OrganizationId == organizationId, cancellationToken),
            await runsQuery.CountAsync(cancellationToken),
            await runsQuery.CountAsync(x => x.Status == Domain.RunStatus.Failed, cancellationToken),
            await runsQuery.SumAsync(x => x.PromptTokens + x.CompletionTokens, cancellationToken),
            await runsQuery.SumAsync(x => x.EstimatedCostUsd, cancellationToken),
            await runsTodayQuery.CountAsync(cancellationToken),
            await runsTodayQuery.CountAsync(x => x.Status == Domain.RunStatus.Failed, cancellationToken),
            await runsTodayQuery.SumAsync(x => x.PromptTokens + x.CompletionTokens, cancellationToken),
            await runsTodayQuery.SumAsync(x => x.EstimatedCostUsd, cancellationToken));

        var recentRuns = await runsQuery
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(10)
            .Select(x => new
            {
                x.Id,
                agentName = x.Agent!.Name,
                status = x.Status,
                x.CreatedAtUtc,
                x.StartedAtUtc,
                x.CompletedAtUtc,
                x.EstimatedCostUsd,
                x.PromptTokens,
                x.CompletionTokens
            })
            .ToListAsync(cancellationToken);

        var start7d = DateTime.UtcNow.Date.AddDays(-6);
        var runs7d = await runsQuery
            .Where(x => x.CreatedAtUtc >= start7d)
            .Select(x => new { x.CreatedAtUtc, x.Status, Tokens = x.PromptTokens + x.CompletionTokens })
            .ToListAsync(cancellationToken);

        var dailyRuns = Enumerable.Range(0, 7)
            .Select(i => start7d.AddDays(i))
            .Select(day => new
            {
                label = day.ToString("ddd", System.Globalization.CultureInfo.GetCultureInfo("fr-FR")),
                success = runs7d.Count(r => r.CreatedAtUtc.Date == day.Date && r.Status == Domain.RunStatus.Completed),
                failed = runs7d.Count(r => r.CreatedAtUtc.Date == day.Date && r.Status == Domain.RunStatus.Failed),
                running = runs7d.Count(r => r.CreatedAtUtc.Date == day.Date && r.Status == Domain.RunStatus.Running),
                queued = runs7d.Count(r => r.CreatedAtUtc.Date == day.Date && r.Status == Domain.RunStatus.Queued)
            })
            .ToList();

        var statusBreakdown = await runsQuery
            .GroupBy(x => x.Status)
            .Select(g => new { status = g.Key.ToString(), count = g.Count() })
            .ToListAsync(cancellationToken);

        var start30d = DateTime.UtcNow.Date.AddDays(-29);
        var tokens30d = await runsQuery
            .Where(x => x.CreatedAtUtc >= start30d)
            .Select(x => new { x.CreatedAtUtc, Tokens = x.PromptTokens + x.CompletionTokens })
            .ToListAsync(cancellationToken);

        var tokenSeries = Enumerable.Range(0, 30)
            .Select(i => start30d.AddDays(i))
            .Select(day => tokens30d.Where(r => r.CreatedAtUtc.Date == day.Date).Sum(r => r.Tokens))
            .ToList();

        var activeAgents = await dbContext.Agents.CountAsync(
            x => x.OrganizationId == organizationId && x.Status == Domain.AgentStatus.Active,
            cancellationToken);

        var runtime = await dbContext.RuntimeHeartbeats
            .OrderByDescending(x => x.LastSeenUtc)
            .Select(x => new RuntimeStatusDto(x.NodeName, x.Status, x.LastSeenUtc, x.ActiveTriggerCount))
            .ToListAsync(cancellationToken);

        return Ok(new { stats, activeAgents, recentRuns, runtime, dailyRuns, statusBreakdown, tokenSeries });
    }

    [HttpGet("runs")]
    public async Task<IActionResult> Runs([FromQuery] int limit = 50, CancellationToken cancellationToken = default)
    {
        var organizationId = tenantService.OrganizationId;
        if (organizationId == Guid.Empty)
            return BadRequest("Missing organization context.");

        limit = Math.Clamp(limit, 1, 200);

        var runs = await dbContext.AgentRuns
            .Where(x => x.OrganizationId == organizationId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(limit)
            .Select(x => new
            {
                x.Id,
                x.AgentId,
                agentName = x.Agent!.Name,
                status = x.Status,
                x.CreatedAtUtc,
                x.EstimatedCostUsd,
                x.PromptTokens,
                x.CompletionTokens,
                x.ErrorMessage
            })
            .ToListAsync(cancellationToken);

        return Ok(runs);
    }
}

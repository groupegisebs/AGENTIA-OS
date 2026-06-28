using AgenticFactory.Application;
using AgenticFactory.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AgenticFactory.Api.Controllers;

[ApiController]
[Route("api/agents")]
public class AgentsController(
    ICurrentTenantService tenantService,
    IAgentDeploymentService deploymentService,
    IAgentInvocationService invocationService,
    AgenticFactoryDbContext dbContext) : ControllerBase
{
    [HttpGet]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "CanViewDashboard")]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var organizationId = tenantService.OrganizationId;
        if (organizationId == Guid.Empty)
            return BadRequest("Missing organization context.");

        var sevenDaysAgo = DateTime.UtcNow.Date.AddDays(-6);
        var thirtyDaysAgo = DateTime.UtcNow.Date.AddDays(-29);

        var agents = await dbContext.Agents
            .Where(x => x.OrganizationId == organizationId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var agentIds = agents.Select(x => x.Id).ToList();

        var runs = await dbContext.AgentRuns
            .Where(x => x.OrganizationId == organizationId && agentIds.Contains(x.AgentId))
            .Select(x => new { x.AgentId, x.Status, x.CreatedAtUtc, x.EstimatedCostUsd })
            .ToListAsync(cancellationToken);

        var activeDeployments = await dbContext.AgentDeployments
            .Where(x => x.OrganizationId == organizationId
                && agentIds.Contains(x.AgentId)
                && x.Status == Domain.DeploymentStatus.Active)
            .Include(x => x.AgentVersion)
            .ToListAsync(cancellationToken);

        var blueprints = await dbContext.AgentBlueprints
            .Where(x => x.OrganizationId == organizationId && agentIds.Contains(x.AgentId))
            .GroupBy(x => x.AgentId)
            .Select(g => new { AgentId = g.Key, BlueprintId = g.OrderByDescending(b => b.CreatedAtUtc).Select(b => b.Id).First() })
            .ToDictionaryAsync(x => x.AgentId, x => x.BlueprintId, cancellationToken);

        var runningAgentIds = runs
            .Where(x => x.Status == Domain.RunStatus.Running)
            .Select(x => x.AgentId)
            .ToHashSet();

        var items = agents.Select(a =>
        {
            var agentRuns = runs.Where(r => r.AgentId == a.Id).ToList();
            var deployment = activeDeployments
                .Where(d => d.AgentId == a.Id)
                .OrderByDescending(d => d.ActivatedAtUtc)
                .FirstOrDefault();
            var sparkline = Enumerable.Range(0, 7)
                .Select(i => sevenDaysAgo.AddDays(i))
                .Select(day => agentRuns.Count(r => r.CreatedAtUtc.Date == day.Date))
                .ToArray();

            return new
            {
                a.Id,
                a.Name,
                a.Description,
                a.EndpointSlug,
                status = a.Status.ToString(),
                displayStatus = runningAgentIds.Contains(a.Id) ? "Running" : a.Status.ToString(),
                a.CreatedAtUtc,
                latestBlueprintId = blueprints.TryGetValue(a.Id, out var bp) ? bp : (Guid?)null,
                versionNumber = deployment?.AgentVersion?.VersionNumber,
                environment = deployment?.Environment ?? "—",
                lastRunAt = agentRuns.OrderByDescending(r => r.CreatedAtUtc).Select(r => (DateTime?)r.CreatedAtUtc).FirstOrDefault(),
                runsLast7Days = agentRuns.Count(r => r.CreatedAtUtc >= sevenDaysAgo),
                runsLast30Days = agentRuns.Count(r => r.CreatedAtUtc >= thirtyDaysAgo),
                costLast30Days = agentRuns.Where(r => r.CreatedAtUtc >= thirtyDaysAgo).Sum(r => r.EstimatedCostUsd),
                runsSparkline = sparkline
            };
        }).ToList();

        var summary = new
        {
            total = agents.Count,
            active = agents.Count(x => x.Status == Domain.AgentStatus.Active),
            running = runningAgentIds.Count(id => agentIds.Contains(id)),
            paused = agents.Count(x => x.Status == Domain.AgentStatus.Draft),
            disabled = agents.Count(x => x.Status == Domain.AgentStatus.Disabled)
        };

        return Ok(new { summary, agents = items });
    }

    [HttpGet("deployments")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "CanViewDashboard")]
    public async Task<IActionResult> ListDeployments(CancellationToken cancellationToken)
    {
        var organizationId = tenantService.OrganizationId;
        if (organizationId == Guid.Empty)
            return BadRequest("Missing organization context.");

        var deployments = await dbContext.AgentDeployments
            .Where(x => x.OrganizationId == organizationId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => new
            {
                x.Id,
                x.AgentId,
                agentName = x.Agent!.Name,
                endpointSlug = x.Agent!.EndpointSlug,
                versionNumber = x.AgentVersion!.VersionNumber,
                status = x.Status.ToString(),
                x.Environment,
                x.ActivatedAtUtc,
                x.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);

        return Ok(deployments);
    }

    [HttpPost("deploy")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "CanCreateAgent")]
    public async Task<IActionResult> Deploy(DeployAgentRequest request, CancellationToken cancellationToken)
    {
        var organizationId = tenantService.OrganizationId;
        if (organizationId == Guid.Empty)
        {
            return BadRequest("Missing organization context.");
        }

        var result = await deploymentService.DeployAsync(organizationId, request, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{endpointSlug}/invoke")]
    [AllowAnonymous]
    public async Task<IActionResult> Invoke(string endpointSlug, InvokeAgentRequest request, CancellationToken cancellationToken)
    {
        var organizationId = tenantService.OrganizationId;
        if (organizationId == Guid.Empty)
        {
            return BadRequest("Missing organization context.");
        }

        var apiKey = Request.Headers["X-Agent-Key"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return Unauthorized("Missing X-Agent-Key header.");
        }

        try
        {
            var result = await invocationService.InvokeAsync(organizationId, endpointSlug, apiKey, request, cancellationToken);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ex.Message);
        }
    }
}

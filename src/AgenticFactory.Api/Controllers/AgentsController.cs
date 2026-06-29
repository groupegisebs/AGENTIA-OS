using AgenticFactory.Application;
using AgenticFactory.Infrastructure.Billing;
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

    [HttpGet("{agentId:guid}/deployments/detail")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "CanViewDashboard")]
    public async Task<IActionResult> DeploymentDetail(Guid agentId, CancellationToken cancellationToken)
    {
        var organizationId = tenantService.OrganizationId;
        if (organizationId == Guid.Empty)
            return BadRequest("Missing organization context.");

        var agent = await dbContext.Agents
            .FirstOrDefaultAsync(x => x.Id == agentId && x.OrganizationId == organizationId, cancellationToken);
        if (agent is null)
            return NotFound();

        var thirtyDaysAgo = DateTime.UtcNow.Date.AddDays(-29);
        var deployments = await dbContext.AgentDeployments
            .Include(x => x.AgentVersion)
            .Where(x => x.AgentId == agentId && x.OrganizationId == organizationId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var versions = await dbContext.AgentVersions
            .Where(x => x.AgentId == agentId && x.OrganizationId == organizationId)
            .OrderByDescending(x => x.VersionNumber)
            .ToListAsync(cancellationToken);

        var blueprints = await dbContext.AgentBlueprints
            .Where(x => x.AgentId == agentId && x.OrganizationId == organizationId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var runs = await dbContext.AgentRuns
            .Where(x => x.AgentId == agentId && x.OrganizationId == organizationId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(50)
            .ToListAsync(cancellationToken);

        var runs30d = runs.Where(x => x.CreatedAtUtc >= thirtyDaysAgo).ToList();
        var triggers = await dbContext.AgentTriggers
            .Where(x => x.AgentId == agentId && x.OrganizationId == organizationId)
            .ToListAsync(cancellationToken);

        var runtime = await dbContext.RuntimeHeartbeats
            .OrderByDescending(x => x.LastSeenUtc)
            .FirstOrDefaultAsync(cancellationToken);

        var activeDeployment = deployments
            .FirstOrDefault(x => x.Status == Domain.DeploymentStatus.Active
                && x.Environment.Equals("production", StringComparison.OrdinalIgnoreCase))
            ?? deployments.FirstOrDefault(x => x.Status == Domain.DeploymentStatus.Active);

        var currentVersion = activeDeployment?.AgentVersion
            ?? versions.FirstOrDefault(v => v.Id == agent.ActiveVersionId)
            ?? versions.FirstOrDefault();

        var versionRows = versions.Select(v =>
        {
            var dep = deployments.FirstOrDefault(d => d.AgentVersionId == v.Id && d.Status == Domain.DeploymentStatus.Active);
            var bp = blueprints.FirstOrDefault();
            return new
            {
                v.Id,
                versionNumber = v.VersionNumber,
                label = $"v1.{v.VersionNumber}.0",
                description = bp?.PromptSummary ?? agent.Description,
                v.CreatedAtUtc,
                createdBy = "—",
                isCurrent = currentVersion?.Id == v.Id,
                status = dep is not null ? "Deployed" : "Ready"
            };
        }).ToList();

        var pipelineStages = new[] { "development", "staging", "production" };
        var pipeline = pipelineStages.Select(stage =>
        {
            var dep = deployments
                .Where(d => d.Environment.Equals(stage, StringComparison.OrdinalIgnoreCase)
                    || (stage == "development" && d.Environment.Equals("dev", StringComparison.OrdinalIgnoreCase)))
                .OrderByDescending(d => d.ActivatedAtUtc)
                .FirstOrDefault();
            return new
            {
                stage,
                label = stage switch { "development" => "Développement", "staging" => "Staging", _ => "Production" },
                status = dep?.Status == Domain.DeploymentStatus.Active ? "Active" : "Inactive",
                deployedAt = dep?.ActivatedAtUtc,
                versionNumber = dep?.AgentVersion?.VersionNumber,
                versionLabel = dep?.AgentVersion is not null ? $"v1.{dep.AgentVersion.VersionNumber}.0" : null
            };
        }).ToList();

        var logEntries = new List<(DateTime At, string Level, string Message)>();
        foreach (var dep in deployments.Take(8))
        {
            logEntries.Add((
                dep.ActivatedAtUtc ?? dep.CreatedAtUtc,
                dep.Status == Domain.DeploymentStatus.Failed ? "ERROR" : "INFO",
                dep.Status == Domain.DeploymentStatus.Active
                    ? $"Déploiement {dep.Environment} v1.{dep.AgentVersion?.VersionNumber}.0 terminé avec succès"
                    : $"Déploiement {dep.Environment} — {dep.Status}"));
        }
        foreach (var run in runs.Take(5))
        {
            logEntries.Add((
                run.CreatedAtUtc,
                run.Status == Domain.RunStatus.Failed ? "ERROR" : "DEBUG",
                run.Status == Domain.RunStatus.Failed
                    ? $"Exécution échouée : {run.ErrorMessage}"
                    : $"Exécution {run.Status} — {run.PromptTokens + run.CompletionTokens} tokens"));
        }
        var operationLogs = logEntries
            .OrderByDescending(x => x.At)
            .Take(12)
            .Select(x => new { at = x.At, level = x.Level, message = x.Message })
            .ToList();

        var recentTimeline = deployments.Take(6).Select(d => new
        {
            d.Environment,
            versionLabel = d.AgentVersion is not null ? $"v1.{d.AgentVersion.VersionNumber}.0" : "—",
            at = d.ActivatedAtUtc ?? d.CreatedAtUtc,
            outcome = d.Status switch
            {
                Domain.DeploymentStatus.Active => "Succès",
                Domain.DeploymentStatus.Failed => "Échec",
                _ => "En attente"
            }
        }).ToList();

        var uptimeSince = activeDeployment?.ActivatedAtUtc ?? agent.CreatedAtUtc;
        var uptime = DateTime.UtcNow - uptimeSince;

        return Ok(new
        {
            agent = new
            {
                agent.Id,
                agent.Name,
                agent.Description,
                agent.EndpointSlug,
                status = agent.Status.ToString(),
                invokeUrl = $"/api/agents/{agent.EndpointSlug}/invoke"
            },
            currentVersion = currentVersion is null ? null : new
            {
                currentVersion.Id,
                currentVersion.VersionNumber,
                label = $"v1.{currentVersion.VersionNumber}.0"
            },
            pipeline,
            versions = versionRows,
            production = activeDeployment is null ? null : new
            {
                activeDeployment.Environment,
                status = activeDeployment.Status.ToString(),
                activeDeployment.ActivatedAtUtc,
                apiKeyMasked = MaskApiKey(activeDeployment.ApiKeyHash),
                runtimeNode = runtime?.NodeName ?? "—",
                runtimeStatus = runtime?.Status ?? "Unknown",
                uptimeHours = (int)uptime.TotalHours,
                uptimeDays = (int)uptime.TotalDays,
                triggers = triggers.Select(t => new { type = t.Type.ToString(), t.IsEnabled }).ToList()
            },
            usage = new
            {
                runs = runs30d.Count,
                tokens = runs30d.Sum(r => r.PromptTokens + r.CompletionTokens),
                cost = runs30d.Sum(r => r.EstimatedCostUsd),
                errors = runs30d.Count(r => r.Status == Domain.RunStatus.Failed),
                tokenSeries = Enumerable.Range(0, 30)
                    .Select(i => thirtyDaysAgo.AddDays(i))
                    .Select(day => runs30d.Where(r => r.CreatedAtUtc.Date == day.Date).Sum(r => r.PromptTokens + r.CompletionTokens))
                    .ToList()
            },
            recentTimeline,
            operationLogs,
            latestBlueprintId = blueprints.FirstOrDefault()?.Id
        });
    }

    private static string MaskApiKey(string hash) =>
        hash.Length >= 8 ? $"{hash[..4]}****{hash[^4..]}" : "****";

    [HttpPost("deploy")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "CanCreateAgent")]
    public async Task<IActionResult> Deploy(DeployAgentRequest request, CancellationToken cancellationToken)
    {
        var organizationId = tenantService.OrganizationId;
        if (organizationId == Guid.Empty)
        {
            return BadRequest("Missing organization context.");
        }

        try
        {
            var result = await deploymentService.DeployAsync(organizationId, request, cancellationToken);
            return Ok(result);
        }
        catch (PublishPaymentRequiredException ex)
        {
            var e = ex.Eligibility;
            return StatusCode(StatusCodes.Status402PaymentRequired, new
            {
                code = e.BlockReason,
                message = e.MessageFr,
                ctaLabel = e.CtaLabelFr,
                checkoutAction = e.CheckoutAction,
                requiredAmountUsd = e.RequiredAmountUsd,
                subscriptionPlanId = e.SubscriptionPlanId,
                publishCreditsBalance = e.PublishCreditsBalance,
                deployedAgents = e.DeployedAgents,
                maxAgents = e.MaxAgents
            });
        }
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

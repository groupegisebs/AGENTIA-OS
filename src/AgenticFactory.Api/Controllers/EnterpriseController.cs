using System.Text.Json;
using AgenticFactory.Application;
using AgenticFactory.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AgenticFactory.Infrastructure.Persistence;

namespace AgenticFactory.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/enterprise")]
public sealed class EnterpriseController(
    ICurrentTenantService tenant,
    ISecretStore secrets,
    IKnowledgeService knowledge,
    IMarketplaceService marketplace,
    IAgentOptimizationService optimization,
    IObservatoryService observatory,
    AuditService audit,
    AgenticFactoryDbContext db) : ControllerBase
{
    [HttpGet("secrets")]
    public async Task<IActionResult> ListSecrets(CancellationToken ct)
        => Ok(await secrets.ListAsync(tenant.OrganizationId, ct));

    [HttpPost("secrets")]
    public async Task<IActionResult> UpsertSecret([FromBody] UpsertSecretRequest request, CancellationToken ct)
    {
        var id = await secrets.UpsertAsync(tenant.OrganizationId, request.Name, request.Value, request.Provider, ct);
        await audit.WriteAsync(tenant.OrganizationId, "secret.upsert", User.Identity?.Name, "OrganizationSecret", id, new { request.Name }, ct);
        return Ok(new { id });
    }

    [HttpPost("knowledge/bases")]
    public async Task<IActionResult> CreateKnowledgeBase([FromBody] CreateKnowledgeBaseRequest request, CancellationToken ct)
    {
        var id = await knowledge.CreateBaseAsync(tenant.OrganizationId, request.AgentId, request.Name, ct);
        return Ok(new { id });
    }

    [HttpPost("knowledge/documents")]
    public async Task<IActionResult> IngestDocument([FromBody] IngestDocumentRequest request, CancellationToken ct)
    {
        var id = await knowledge.IngestDocumentAsync(tenant.OrganizationId, request.KnowledgeBaseId, request.Title, request.Content, ct);
        return Ok(new { id });
    }

    [HttpGet("knowledge/search")]
    public async Task<IActionResult> Search([FromQuery] Guid agentId, [FromQuery] string q, CancellationToken ct)
        => Ok(await knowledge.SearchAsync(tenant.OrganizationId, agentId, q ?? string.Empty, 8, ct));

    [HttpGet("marketplace")]
    [AllowAnonymous]
    public async Task<IActionResult> Marketplace(CancellationToken ct)
        => Ok(await marketplace.ListPublishedAsync(ct));

    [HttpPost("marketplace/publish")]
    public async Task<IActionResult> Publish([FromBody] MarketplacePublishRequest request, [FromQuery] Guid agentId, CancellationToken ct)
    {
        var dto = await marketplace.PublishAsync(tenant.OrganizationId, agentId, request, ct);
        await audit.WriteAsync(tenant.OrganizationId, "marketplace.publish", User.Identity?.Name, "Agent", agentId, dto, ct);
        return Ok(dto);
    }

    [HttpPost("agents/{agentId:guid}/rollback/{versionNumber:int}")]
    public async Task<IActionResult> Rollback(Guid agentId, int versionNumber, CancellationToken ct)
    {
        await marketplace.RollbackVersionAsync(tenant.OrganizationId, agentId, versionNumber, ct);
        await audit.WriteAsync(tenant.OrganizationId, "agent.rollback", User.Identity?.Name, "Agent", agentId, new { versionNumber }, ct);
        return Ok(new { rolledBackTo = versionNumber });
    }

    [HttpGet("agents/{agentId:guid}/optimize")]
    public async Task<IActionResult> Optimize(Guid agentId, CancellationToken ct)
        => Ok(await optimization.AnalyzeAsync(tenant.OrganizationId, agentId, ct));

    [HttpGet("observatory")]
    public async Task<IActionResult> Observatory(CancellationToken ct)
        => Ok(await observatory.GetSnapshotAsync(tenant.OrganizationId, ct));

    [HttpGet("agents/{agentId:guid}/metrics")]
    public async Task<IActionResult> AgentMetrics(Guid agentId, CancellationToken ct)
    {
        var since = DateTime.UtcNow.AddHours(-24);
        var runs = await db.AgentRuns.AsNoTracking()
            .Where(r => r.OrganizationId == tenant.OrganizationId && r.AgentId == agentId && r.CreatedAtUtc >= since)
            .ToListAsync(ct);
        var logs = await db.ActionExecutionLogs.AsNoTracking()
            .Where(l => l.OrganizationId == tenant.OrganizationId && l.AgentId == agentId && l.CreatedAtUtc >= since)
            .ToListAsync(ct);

        var ok = runs.Count(r => r.Status == Domain.RunStatus.Completed);
        var success = runs.Count == 0 ? 100 : Math.Round(ok * 100.0 / runs.Count, 1);
        var avgMs = logs.Count == 0 ? 0 : logs.Average(l => l.DurationMs);

        return Ok(new
        {
            executions24h = runs.Count,
            successRate = success,
            avgDurationMs = Math.Round(avgMs, 0),
            costUsd = runs.Sum(r => r.EstimatedCostUsd),
            errors = runs.Count(r => r.Status == Domain.RunStatus.Failed),
            tokens = runs.Sum(r => r.PromptTokens + r.CompletionTokens),
            recentSteps = logs.OrderByDescending(l => l.CreatedAtUtc).Take(20).Select(l => new
            {
                l.NodeId, l.NodeType, l.Label, l.Status, l.DurationMs, l.StartedAtUtc, l.ErrorMessage
            })
        });
    }

    [HttpGet("agents/{agentId:guid}/runs/{runId:guid}/timeline")]
    public async Task<IActionResult> RunTimeline(Guid agentId, Guid runId, CancellationToken ct)
    {
        var steps = await db.ActionExecutionLogs.AsNoTracking()
            .Where(l => l.OrganizationId == tenant.OrganizationId && l.AgentId == agentId && l.RunId == runId)
            .OrderBy(l => l.StartedAtUtc)
            .Select(l => new { l.Label, l.NodeType, l.Status, l.DurationMs, l.StartedAtUtc, l.CompletedAtUtc, l.ErrorMessage })
            .ToListAsync(ct);
        return Ok(steps);
    }

    [HttpGet("audit")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Audit([FromQuery] int take = 50, CancellationToken ct = default)
    {
        var rows = await db.AuditLogEntries.AsNoTracking()
            .Where(x => x.OrganizationId == tenant.OrganizationId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(Math.Clamp(take, 1, 200))
            .ToListAsync(ct);
        return Ok(rows);
    }

    [HttpPut("agents/{agentId:guid}/personality")]
    public async Task<IActionResult> UpdatePersonality(Guid agentId, [FromBody] PersonalityRequest request, CancellationToken ct)
    {
        var agent = await db.Agents.FirstAsync(a => a.Id == agentId && a.OrganizationId == tenant.OrganizationId, ct);
        agent.AvatarEmoji = request.AvatarEmoji ?? agent.AvatarEmoji;
        agent.PersonalityStyle = request.Style ?? agent.PersonalityStyle;
        agent.PersonalityTemperature = request.Temperature;
        agent.PersonalityLanguages = request.Languages ?? agent.PersonalityLanguages;
        agent.KpisJson = request.KpisJson ?? agent.KpisJson;
        agent.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Ok(new { agent.Id, agent.AvatarEmoji, agent.PersonalityStyle, agent.PersonalityTemperature });
    }
    [HttpPost("graph/dry-run")]
    public async Task<IActionResult> DryRun([FromBody] GraphDryRunRequest request, [FromServices] IGraphRuntimeEngine graph, CancellationToken ct)
    {
        var input = request.Input ?? new Dictionary<string, object?>();
        var result = await graph.ExecuteDryAsync(tenant.OrganizationId, request.CapabilityGraphJson, input, ct);
        return Ok(result);
    }
}

public sealed record UpsertSecretRequest(string Name, string Value, string? Provider);
public sealed record CreateKnowledgeBaseRequest(Guid AgentId, string Name);
public sealed record IngestDocumentRequest(Guid KnowledgeBaseId, string Title, string Content);
public sealed record PersonalityRequest(string? AvatarEmoji, string? Style, double Temperature, string? Languages, string? KpisJson);
public sealed record GraphDryRunRequest(string CapabilityGraphJson, Dictionary<string, object?>? Input);

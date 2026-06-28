using AgenticFactory.Application;
using AgenticFactory.Domain;
using AgenticFactory.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace AgenticFactory.Api.Controllers;

[ApiController]
[Route("api/studio")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class StudioController(
    AgenticFactoryDbContext db,
    ICurrentTenantService tenantService,
    IConfiguration configuration,
    ILogger<StudioController> logger) : ControllerBase
{
    [HttpPost("domain-requests")]
    [Authorize(Policy = "CanCreateAgent")]
    public async Task<IActionResult> SubmitDomainRequest(
        SubmitDomainRequestDto request,
        CancellationToken cancellationToken)
    {
        var organizationId = tenantService.OrganizationId;
        if (organizationId == Guid.Empty)
            return BadRequest(new { message = "Contexte organisation manquant." });

        if (string.IsNullOrWhiteSpace(request.DomainName))
            return BadRequest(new { message = "Le nom du domaine est requis." });

        var email = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value
            ?? User.Identity?.Name
            ?? "unknown@agentia.local";
        var name = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value
            ?? User.FindFirst("full_name")?.Value
            ?? email;

        var entity = new StudioDomainRequest
        {
            OrganizationId = organizationId,
            RequestedByEmail = email,
            RequestedByName = name,
            DomainName = request.DomainName.Trim(),
            Industry = request.Industry?.Trim(),
            UseCase = request.UseCase?.Trim(),
            Description = request.Description?.Trim(),
            Status = DomainRequestStatus.Pending
        };

        db.StudioDomainRequests.Add(entity);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Domain request {RequestId} submitted by {Email} for « {DomainName} » (org {OrgId})",
            entity.Id, email, entity.DomainName, organizationId);

        return Ok(new
        {
            id = entity.Id,
            status = entity.Status.ToString(),
            message = "Votre demande a été transmise à l'équipe Agentia."
        });
    }

    [HttpGet("domain-requests")]
    [Authorize(Policy = "CanCreateAgent")]
    public async Task<IActionResult> ListDomainRequests(CancellationToken cancellationToken)
    {
        var organizationId = tenantService.OrganizationId;
        if (organizationId == Guid.Empty)
            return BadRequest(new { message = "Contexte organisation manquant." });

        var items = await db.StudioDomainRequests
            .AsNoTracking()
            .Where(x => x.OrganizationId == organizationId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(50)
            .Select(x => new
            {
                x.Id,
                x.DomainName,
                x.Industry,
                x.Status,
                x.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    [HttpPost("objective-requests")]
    [Authorize(Policy = "CanCreateAgent")]
    public async Task<IActionResult> SubmitObjectiveRequest(
        SubmitObjectiveRequestDto request,
        CancellationToken cancellationToken)
    {
        var organizationId = tenantService.OrganizationId;
        if (organizationId == Guid.Empty)
            return BadRequest(new { message = "Contexte organisation manquant." });

        if (string.IsNullOrWhiteSpace(request.ObjectiveName))
            return BadRequest(new { message = "Le nom de l'objectif est requis." });

        var email = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value
            ?? User.Identity?.Name
            ?? "unknown@agentia.local";
        var name = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value
            ?? User.FindFirst("full_name")?.Value
            ?? email;

        var entity = new StudioObjectiveRequest
        {
            OrganizationId = organizationId,
            RequestedByEmail = email,
            RequestedByName = name,
            ObjectiveName = request.ObjectiveName.Trim(),
            RelatedDomain = request.RelatedDomain?.Trim(),
            UseCase = request.UseCase?.Trim(),
            Description = request.Description?.Trim(),
            Status = DomainRequestStatus.Pending
        };

        db.StudioObjectiveRequests.Add(entity);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Objective request {RequestId} submitted by {Email} for « {ObjectiveName} » (org {OrgId})",
            entity.Id, email, entity.ObjectiveName, organizationId);

        return Ok(new
        {
            id = entity.Id,
            status = entity.Status.ToString(),
            message = "Votre demande d'objectif a été transmise à l'équipe Agentia."
        });
    }

    [HttpPost("estimate")]
    [Authorize(Policy = "CanCreateAgent")]
    public async Task<IActionResult> EstimateBlueprint(
        StudioEstimateRequestDto request,
        CancellationToken cancellationToken)
    {
        var organizationId = tenantService.OrganizationId;
        if (organizationId == Guid.Empty)
            return BadRequest(new { message = "Contexte organisation manquant." });

        var complexity = ComputeComplexity(
            request.HasDomain,
            request.ObjectiveCount,
            request.SourceCount,
            request.ActionCount,
            request.AutonomyLevel);

        var aiModel = configuration["AI:OpenAI:Model"]
            ?? configuration["AI:AzureOpenAI:Deployment"]
            ?? "gpt-4o-mini";

        var runsPerMonth = EstimateRunsPerMonth(request.TriggerId, request.TriggerFrequency);
        var promptTokensPerRun = 400
            + request.ObjectiveCount * 80
            + request.SourceCount * 120
            + request.ActionCount * 100;
        var completionTokensPerRun = (int)(promptTokensPerRun * 0.5) + 200;

        var promptRate = ParseDecimal(configuration["AI:Pricing:PromptPer1kUsd"], 0.00015m);
        var completionRate = ParseDecimal(configuration["AI:Pricing:CompletionPer1kUsd"], 0.0006m);
        var costPerRun = (promptTokensPerRun / 1000m) * promptRate
            + (completionTokensPerRun / 1000m) * completionRate;
        costPerRun *= 1 + request.AutonomyLevel * 0.15m;
        costPerRun *= GetRuntimeMultiplier(request.RuntimeId);

        var monthlyCost = costPerRun * runsPerMonth;
        var costBasis = "pricing";

        var planFees = await db.OrganizationSubscriptions
            .AsNoTracking()
            .Where(x => x.OrganizationId == organizationId && x.IsActive)
            .Select(x => new { x.SubscriptionPlan!.BlueprintCreationFeeUsd, x.SubscriptionPlan.DeployFeeUsd })
            .FirstOrDefaultAsync(cancellationToken);

        var creationPromptTokens = 800 + request.ObjectiveCount * 100 + request.ActionCount * 80;
        var creationCompletionTokens = (int)(creationPromptTokens * 0.6) + 400;
        var creationCostUsd = (creationPromptTokens / 1000m) * promptRate
            + (creationCompletionTokens / 1000m) * completionRate
            + (planFees?.BlueprintCreationFeeUsd ?? 0m);

        var thirtyDaysAgo = DateTime.UtcNow.Date.AddDays(-29);
        var orgRunCosts = await db.AgentRuns
            .AsNoTracking()
            .Where(x => x.OrganizationId == organizationId && x.CreatedAtUtc >= thirtyDaysAgo)
            .Select(x => x.EstimatedCostUsd)
            .ToListAsync(cancellationToken);

        if (orgRunCosts.Count >= 3)
        {
            var historicalMonthly = orgRunCosts.Average() * runsPerMonth;
            monthlyCost = (monthlyCost + historicalMonthly) / 2m;
            costBasis = "blended";
        }

        return Ok(new
        {
            complexity,
            estimatedMonthlyCostUsd = Math.Round(monthlyCost, 2),
            creationCostUsd = Math.Round(creationCostUsd, 4),
            deployFeeUsd = planFees?.DeployFeeUsd ?? 0m,
            aiModel,
            costBasis,
            costLabel = costBasis switch
            {
                "blended" => "Estimation (tarifs + historique org.)",
                _ => "Estimation (tarifs configurés)"
            }
        });
    }

    private static int ComputeComplexity(
        bool hasDomain, int objectives, int sources, int actions, int autonomy)
    {
        var score = (hasDomain ? 2 : 0)
            + objectives * 0.5
            + sources * 0.25
            + actions * 0.3
            + autonomy * 0.35;
        return Math.Min(5, Math.Max(0, (int)Math.Round(score)));
    }

    private static int EstimateRunsPerMonth(string? triggerId, string? frequency)
    {
        if (string.Equals(triggerId, "scheduled", StringComparison.OrdinalIgnoreCase))
        {
            return frequency switch
            {
                "5min" => 8640,
                "15min" => 2880,
                "hourly" => 720,
                "daily" => 30,
                _ => 120
            };
        }

        if (string.Equals(triggerId, "webhook", StringComparison.OrdinalIgnoreCase)
            || string.Equals(triggerId, "api", StringComparison.OrdinalIgnoreCase))
            return 300;

        return 30;
    }

    private static decimal GetRuntimeMultiplier(string? runtimeId) => runtimeId switch
    {
        "kubernetes" => 1.35m,
        "azure" or "aws" or "ovh" => 1.25m,
        "docker" => 1.15m,
        _ => 1m
    };

    private static decimal ParseDecimal(string? value, decimal fallback) =>
        decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;
}

public sealed record SubmitDomainRequestDto(
    string DomainName,
    string? Industry,
    string? UseCase,
    string? Description);

public sealed record SubmitObjectiveRequestDto(
    string ObjectiveName,
    string? RelatedDomain,
    string? UseCase,
    string? Description);

public sealed record StudioEstimateRequestDto(
    bool HasDomain,
    int ObjectiveCount,
    int SourceCount,
    int ActionCount,
    int AutonomyLevel,
    string? TriggerId,
    string? TriggerFrequency,
    string? RuntimeId,
    bool HeartbeatEnabled);

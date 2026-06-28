using AgenticFactory.Application;
using AgenticFactory.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AgenticFactory.Api.Controllers;

[ApiController]
[Route("api/billing")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "CanViewDashboard")]
public class BillingController(
    AgenticFactoryDbContext dbContext,
    ICurrentTenantService tenantService) : ControllerBase
{
    [HttpGet("subscription")]
    public async Task<IActionResult> Subscription(CancellationToken cancellationToken)
    {
        var organizationId = tenantService.OrganizationId;
        if (organizationId == Guid.Empty)
            return BadRequest("Missing organization context.");

        var subscription = await dbContext.OrganizationSubscriptions
            .AsNoTracking()
            .Include(x => x.SubscriptionPlan)
            .FirstOrDefaultAsync(x => x.OrganizationId == organizationId && x.IsActive, cancellationToken);

        if (subscription?.SubscriptionPlan is null)
        {
            return Ok(new
            {
                hasSubscription = false,
                planName = "Aucun",
                maxAgents = 0,
                maxRunsPerMonth = 0,
                monthlyPriceUsd = 0m,
                usedRunsThisMonth = 0,
                currentAgents = await dbContext.Agents.CountAsync(x => x.OrganizationId == organizationId, cancellationToken),
                periodStartUtc = (DateTime?)null,
                periodEndUtc = (DateTime?)null
            });
        }

        var currentAgents = await dbContext.Agents.CountAsync(
            x => x.OrganizationId == organizationId, cancellationToken);

        return Ok(new
        {
            hasSubscription = true,
            planName = subscription.SubscriptionPlan.Name,
            maxAgents = subscription.SubscriptionPlan.MaxAgents,
            maxRunsPerMonth = subscription.SubscriptionPlan.MaxRunsPerMonth,
            monthlyPriceUsd = subscription.SubscriptionPlan.MonthlyPriceUsd,
            usedRunsThisMonth = subscription.UsedRunsThisMonth,
            currentAgents,
            periodStartUtc = subscription.PeriodStartUtc,
            periodEndUtc = subscription.PeriodEndUtc
        });
    }

    [HttpGet("plans")]
    public async Task<IActionResult> Plans(CancellationToken cancellationToken)
    {
        var plans = await dbContext.SubscriptionPlans
            .AsNoTracking()
            .OrderBy(x => x.MonthlyPriceUsd)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.MaxAgents,
                x.MaxRunsPerMonth,
                x.MonthlyPriceUsd,
                x.BlueprintCreationFeeUsd,
                x.DeployFeeUsd
            })
            .ToListAsync(cancellationToken);

        return Ok(plans);
    }

    [HttpGet("usage")]
    public async Task<IActionResult> Usage(CancellationToken cancellationToken)
    {
        var organizationId = tenantService.OrganizationId;
        if (organizationId == Guid.Empty)
            return BadRequest("Missing organization context.");

        var now = DateTime.UtcNow;
        var startOfMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var start30d = now.Date.AddDays(-29);

        var runsQuery = dbContext.AgentRuns.Where(x => x.OrganizationId == organizationId);
        var monthRuns = runsQuery.Where(x => x.CreatedAtUtc >= startOfMonth);
        var monthBlueprints = dbContext.AgentBlueprints
            .Where(x => x.OrganizationId == organizationId && x.CreatedAtUtc >= startOfMonth);
        var monthDeployments = dbContext.AgentDeployments
            .Where(x => x.OrganizationId == organizationId && x.CreatedAtUtc >= startOfMonth);

        var runsThisMonth = await monthRuns.CountAsync(cancellationToken);
        var tokensThisMonth = await monthRuns.SumAsync(x => x.PromptTokens + x.CompletionTokens, cancellationToken);
        var aiRunCostThisMonth = await monthRuns.SumAsync(x => x.EstimatedCostUsd, cancellationToken);
        var creationCostThisMonth = await monthBlueprints.SumAsync(x => x.CreationCostUsd, cancellationToken);
        var deployCostThisMonth = await monthDeployments.SumAsync(x => x.DeployFeeUsd, cancellationToken);
        var costThisMonth = aiRunCostThisMonth + creationCostThisMonth + deployCostThisMonth;
        var failedThisMonth = await monthRuns.CountAsync(x => x.Status == Domain.RunStatus.Failed, cancellationToken);
        var blueprintsThisMonth = await monthBlueprints.CountAsync(cancellationToken);
        var deploymentsThisMonth = await monthDeployments.CountAsync(cancellationToken);

        var subscription = await dbContext.OrganizationSubscriptions
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.OrganizationId == organizationId && x.IsActive, cancellationToken);

        var tokens30d = await runsQuery
            .Where(x => x.CreatedAtUtc >= start30d)
            .Select(x => new { x.CreatedAtUtc, Tokens = x.PromptTokens + x.CompletionTokens })
            .ToListAsync(cancellationToken);

        var tokenSeries = Enumerable.Range(0, 30)
            .Select(i => start30d.AddDays(i))
            .Select(day => tokens30d.Where(r => r.CreatedAtUtc.Date == day.Date).Sum(r => r.Tokens))
            .ToList();

        var runs30d = await runsQuery
            .Where(x => x.CreatedAtUtc >= start30d)
            .GroupBy(x => x.CreatedAtUtc.Date)
            .Select(g => new { day = g.Key, count = g.Count() })
            .ToListAsync(cancellationToken);

        var runSeries = Enumerable.Range(0, 30)
            .Select(i => start30d.AddDays(i))
            .Select(day => runs30d.FirstOrDefault(r => r.day == day.Date)?.count ?? 0)
            .ToList();

        var cost30d = await runsQuery
            .Where(x => x.CreatedAtUtc >= start30d)
            .SumAsync(x => x.EstimatedCostUsd, cancellationToken);

        var creationCostTotal = await dbContext.AgentBlueprints
            .Where(x => x.OrganizationId == organizationId)
            .SumAsync(x => x.CreationCostUsd, cancellationToken);
        var deployCostTotal = await dbContext.AgentDeployments
            .Where(x => x.OrganizationId == organizationId)
            .SumAsync(x => x.DeployFeeUsd, cancellationToken);
        var aiRunCostTotal = await runsQuery.SumAsync(x => x.EstimatedCostUsd, cancellationToken);
        var totalCost = aiRunCostTotal + creationCostTotal + deployCostTotal;

        var totalTokens = await runsQuery.SumAsync(x => x.PromptTokens + x.CompletionTokens, cancellationToken);
        var totalRuns = await runsQuery.CountAsync(cancellationToken);

        return Ok(new
        {
            runsThisMonth,
            tokensThisMonth,
            costThisMonth,
            aiRunCostThisMonth,
            creationCostThisMonth,
            deployCostThisMonth,
            blueprintsThisMonth,
            deploymentsThisMonth,
            failedThisMonth,
            usedRunsQuota = subscription?.UsedRunsThisMonth ?? runsThisMonth,
            maxRunsPerMonth = subscription?.SubscriptionPlanId is not null
                ? await dbContext.SubscriptionPlans
                    .Where(p => p.Id == subscription.SubscriptionPlanId)
                    .Select(p => p.MaxRunsPerMonth)
                    .FirstOrDefaultAsync(cancellationToken)
                : 0,
            tokenSeries,
            runSeries,
            periodLabel = now.ToString("MMMM yyyy", System.Globalization.CultureInfo.GetCultureInfo("fr-FR")),
            totals = new
            {
                totalRuns,
                totalTokens,
                totalCost,
                aiRunCostTotal,
                creationCostTotal,
                deployCostTotal
            }
        });
    }

    [HttpGet("summary")]
    public async Task<IActionResult> Summary(CancellationToken cancellationToken)
    {
        var organizationId = tenantService.OrganizationId;
        if (organizationId == Guid.Empty)
            return BadRequest("Missing organization context.");

        var subscription = await dbContext.OrganizationSubscriptions
            .AsNoTracking()
            .Include(x => x.SubscriptionPlan)
            .FirstOrDefaultAsync(x => x.OrganizationId == organizationId && x.IsActive, cancellationToken);

        var org = await dbContext.Organizations
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == organizationId, cancellationToken);

        return Ok(new
        {
            organizationName = org?.Name ?? "Organisation",
            planName = subscription?.SubscriptionPlan?.Name ?? "Aucun",
            monthlyPriceUsd = subscription?.SubscriptionPlan?.MonthlyPriceUsd ?? 0m,
            periodStartUtc = subscription?.PeriodStartUtc,
            periodEndUtc = subscription?.PeriodEndUtc,
            hasPaymentMethod = false,
            paymentMethodLabel = (string?)null,
            invoices = Array.Empty<object>()
        });
    }
}

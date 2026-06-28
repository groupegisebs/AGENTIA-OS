using AgenticFactory.Application;
using AgenticFactory.Infrastructure.Billing;
using AgenticFactory.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace AgenticFactory.Api.Controllers;

[ApiController]
[Route("api/billing")]
public class BillingController(
    AgenticFactoryDbContext dbContext,
    ICurrentTenantService tenantService,
    ISubscriptionBillingService billingService,
    IOptions<GisebsApiPayGatewayOptions> payGatewayOptions) : ControllerBase
{
    [HttpGet("subscription")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "CanViewDashboard")]
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
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "CanViewDashboard")]
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
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "CanViewDashboard")]
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
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "CanViewDashboard")]
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

    [HttpGet("payment-config")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "CanViewDashboard")]
    public IActionResult PaymentConfig()
    {
        var configured = payGatewayOptions.Value.IsConfigured;
        return Ok(new
        {
            isConfigured = configured,
            message = configured
                ? (string?)null
                : "Le paiement en ligne n'est pas configuré. Contactez support@agentia.io."
        });
    }

    [HttpPost("checkout")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "CanViewDashboard")]
    public async Task<IActionResult> Checkout([FromBody] CheckoutRequest request, CancellationToken cancellationToken)
    {
        var organizationId = tenantService.OrganizationId;
        if (organizationId == Guid.Empty)
            return BadRequest(new { message = "Contexte organisation manquant." });

        if (request.SubscriptionPlanId == Guid.Empty)
            return BadRequest(new { message = "Plan d'abonnement requis." });

        if (string.IsNullOrWhiteSpace(request.SuccessUrl) || string.IsNullOrWhiteSpace(request.CancelUrl))
            return BadRequest(new { message = "URLs de retour requises." });

        var email = User.FindFirstValue(ClaimTypes.Email)
            ?? await dbContext.ApplicationUsers
                .Where(x => x.OrganizationId == organizationId)
                .Select(x => x.Email)
                .FirstOrDefaultAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(email))
            return BadRequest(new { message = "Adresse courriel introuvable pour l'utilisateur." });

        var displayName = User.FindFirstValue(ClaimTypes.Name) ?? User.Identity?.Name;

        var (result, error) = await billingService.StartCheckoutAsync(
            organizationId,
            new BillingCheckoutRequest(
                request.SubscriptionPlanId,
                email,
                displayName,
                request.SuccessUrl,
                request.CancelUrl),
            cancellationToken);

        if (error is not null)
            return BadRequest(new { message = error });

        return Ok(new
        {
            checkoutId = result!.CheckoutId,
            paymentCode = result.PaymentCode,
            checkoutUrl = result.CheckoutUrl,
            sessionId = result.SessionId,
            status = result.Status
        });
    }

    [HttpPost("payments/confirm")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "CanViewDashboard")]
    public async Task<IActionResult> ConfirmPayment([FromBody] ConfirmPaymentRequest request, CancellationToken cancellationToken)
    {
        var organizationId = tenantService.OrganizationId;
        if (organizationId == Guid.Empty)
            return BadRequest(new { message = "Contexte organisation manquant." });

        var (result, error) = await billingService.ConfirmPaymentAsync(
            organizationId,
            request.PaymentCode,
            request.CheckoutId,
            cancellationToken);

        if (error is not null)
            return BadRequest(new { message = error });

        return Ok(new
        {
            activated = result!.Activated,
            planName = result.PlanName,
            message = result.Message
        });
    }

    /// <summary>
    /// Webhook interne (retour Stripe / automation). Pay Gateway ne pousse pas vers les clients — cet endpoint
    /// valide un secret partagé puis confirme le paiement auprès du gateway.
    /// </summary>
    [HttpPost("webhook")]
    [AllowAnonymous]
    public async Task<IActionResult> Webhook([FromBody] ConfirmPaymentRequest request, CancellationToken cancellationToken)
    {
        var configuredSecret = payGatewayOptions.Value.WebhookSecret;
        if (string.IsNullOrWhiteSpace(configuredSecret))
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "WebhookSecret non configuré." });

        if (!Request.Headers.TryGetValue("X-Webhook-Secret", out var provided)
            || !string.Equals(provided.ToString(), configuredSecret, StringComparison.Ordinal))
        {
            return Unauthorized(new { message = "Secret webhook invalide." });
        }

        var checkout = await dbContext.SubscriptionCheckouts
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                (request.CheckoutId != null && request.CheckoutId != Guid.Empty && x.Id == request.CheckoutId)
                || (!string.IsNullOrWhiteSpace(request.PaymentCode) && x.PaymentCode == request.PaymentCode),
                cancellationToken);

        if (checkout is null)
            return NotFound(new { message = "Session de paiement introuvable." });

        var (result, error) = await billingService.ConfirmPaymentAsync(
            checkout.OrganizationId,
            request.PaymentCode,
            request.CheckoutId,
            cancellationToken);

        if (error is not null)
            return BadRequest(new { message = error });

        return Ok(new
        {
            activated = result!.Activated,
            planName = result.PlanName,
            message = result.Message
        });
    }

    public sealed record CheckoutRequest(Guid SubscriptionPlanId, string SuccessUrl, string CancelUrl);
    public sealed record ConfirmPaymentRequest(string? PaymentCode, Guid? CheckoutId);
}

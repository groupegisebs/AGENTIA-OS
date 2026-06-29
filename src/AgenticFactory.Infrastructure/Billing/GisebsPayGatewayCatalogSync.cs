using System.Net;
using System.Net.Http.Json;
using AgenticFactory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AgenticFactory.Infrastructure.Billing;

/// <summary>
/// Synchronise les plans d'abonnement AGENTIA-OS vers le catalogue Pay Gateway (AGENTIAOS).
/// </summary>
public sealed class GisebsPayGatewayCatalogSync(
    AgenticFactoryDbContext dbContext,
    IHttpClientFactory httpClientFactory,
    IOptions<GisebsApiPayGatewayOptions> options,
    IHostEnvironment environment,
    ILogger<GisebsPayGatewayCatalogSync> logger)
{
    public async Task TrySyncSubscriptionPlansAsync(CancellationToken cancellationToken)
    {
        var config = options.Value;
        if (!config.IsConfigured)
            return;

        try
        {
            config.EnsureSecureEndpoint(environment.IsDevelopment());
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "Pay Gateway : configuration invalide, synchronisation catalogue ignorée.");
            return;
        }

        var plans = await dbContext.SubscriptionPlans
            .AsNoTracking()
            .Where(x => x.MonthlyPriceUsd > 0)
            .OrderBy(x => x.MonthlyPriceUsd)
            .ToListAsync(cancellationToken);

        if (plans.Count == 0)
            return;

        var client = CreateClient(config);
        var synced = 0;

        foreach (var plan in plans)
        {
            var productCode = config.BuildProductCode(plan.Name);
            var request = new CreateCatalogItemRequest(
                productCode,
                $"AGENTIA-OS {plan.Name}",
                $"Abonnement {plan.Name} — {plan.MaxAgents} agents, {plan.MaxRunsPerMonth:N0} runs/mois",
                config.DefaultPlanCode,
                "Mensuel",
                plan.MonthlyPriceUsd,
                "USD",
                SyncToStripe: true);

            HttpResponseMessage response;
            try
            {
                response = await client.PostAsJsonAsync(
                    "api/products/catalog", request, GisebsPayGatewayJson.Options, cancellationToken);
            }
            catch (HttpRequestException ex)
            {
                logger.LogWarning(ex,
                    "Pay Gateway injoignable — synchronisation catalogue ignorée pour {ProductCode}.",
                    productCode);
                return;
            }

            if (response.IsSuccessStatusCode)
            {
                synced++;
                logger.LogInformation("Catalogue Pay Gateway créé pour {ProductCode} / {PlanCode}.",
                    productCode, config.DefaultPlanCode);
                continue;
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (response.StatusCode == HttpStatusCode.BadRequest && LooksLikeAlreadyExists(body))
            {
                synced++;
                logger.LogDebug("Catalogue Pay Gateway déjà présent pour {ProductCode}.", productCode);
                continue;
            }

            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                logger.LogWarning(
                    "Pay Gateway a refusé l'authentification ({Status}) — créez l'application AGENTIAOS "
                    + "dans l'admin Pay Gateway (voir deploy/PAYGATEWAY-CATALOG.md).",
                    (int)response.StatusCode);
                return;
            }

            logger.LogWarning(
                "Pay Gateway a refusé la création catalogue pour {ProductCode} ({Status}) : {Detail}",
                productCode,
                (int)response.StatusCode,
                body.Length > 200 ? body[..200] : body);
        }

        if (synced > 0)
        {
            logger.LogInformation(
                "Synchronisation catalogue Pay Gateway terminée ({Count}/{Total} plans).",
                synced,
                plans.Count);
        }
    }

    private HttpClient CreateClient(GisebsApiPayGatewayOptions config)
    {
        var client = httpClientFactory.CreateClient(nameof(GisebsPayGatewayClient));
        client.BaseAddress = new Uri(config.GetBaseUri().ToString().TrimEnd('/') + "/");
        client.DefaultRequestHeaders.Remove("X-App-Code");
        client.DefaultRequestHeaders.Remove("X-Api-Key");
        client.DefaultRequestHeaders.Add("X-App-Code", config.AppCode);
        client.DefaultRequestHeaders.Add("X-Api-Key", config.ApiKey);
        return client;
    }

    private static bool LooksLikeAlreadyExists(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return false;

        var text = body.ToLowerInvariant();
        return text.Contains("exist", StringComparison.Ordinal)
            || text.Contains("déjà", StringComparison.Ordinal)
            || text.Contains("deja", StringComparison.Ordinal)
            || text.Contains("duplicate", StringComparison.Ordinal);
    }

    private sealed record CreateCatalogItemRequest(
        string ProductCode,
        string ProductName,
        string Description,
        string PlanCode,
        string PlanName,
        decimal Amount,
        string Currency,
        bool SyncToStripe);
}

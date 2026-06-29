using AgenticFactory.Application;
using AgenticFactory.Domain;
using AgenticFactory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AgenticFactory.Infrastructure.Billing;

/// <summary>
/// Crée un produit consommable Pay Gateway pour chaque nouvel agent AGENTIA-OS.
/// </summary>
public sealed class AgentPayGatewayProductService(
    AgenticFactoryDbContext dbContext,
    IGisebsPayGatewayClient payGatewayClient,
    IOptions<GisebsApiPayGatewayOptions> options,
    ILogger<AgentPayGatewayProductService> logger) : IAgentPayGatewayProductService
{
    public async Task<string?> TryEnsureAgentProductAsync(
        Agent agent,
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(agent.PayGatewayProductCode))
            return agent.PayGatewayProductCode;

        if (!payGatewayClient.IsConfigured)
        {
            logger.LogWarning(
                "Pay Gateway non configuré — produit consommable non créé pour l'agent {AgentId} ({AgentName}).",
                agent.Id,
                agent.Name);
            return null;
        }

        var config = options.Value;
        var productCode = config.BuildAgentProductCode(agent.Id);
        var amount = await ResolveAmountUsdAsync(organizationId, cancellationToken);

        var request = new GisebsCatalogItemRequest(
            productCode,
            agent.Name,
            $"Agent consommable AGENTIA-OS — {Truncate(agent.Description, 240)}",
            config.ConsumablePlanCode,
            "Paiement unique",
            amount,
            "USD",
            SyncToStripe: true);

        var result = await payGatewayClient.TryCreateCatalogItemAsync(request, cancellationToken);

        return result.Outcome switch
        {
            GisebsCatalogItemOutcome.Created => LogAndReturn(
                productCode,
                "Produit consommable Pay Gateway créé pour l'agent {AgentId} : {ProductCode}.",
                agent.Id,
                productCode),
            GisebsCatalogItemOutcome.AlreadyExists => LogAndReturn(
                productCode,
                "Produit consommable Pay Gateway déjà présent pour l'agent {AgentId} : {ProductCode}.",
                agent.Id,
                productCode),
            GisebsCatalogItemOutcome.NotConfigured => LogNull(
                "Pay Gateway non configuré — produit consommable ignoré pour l'agent {AgentId}.",
                agent.Id),
            _ => LogNull(
                "Échec création produit Pay Gateway pour l'agent {AgentId} ({ProductCode}) : {Detail}",
                agent.Id,
                productCode,
                result.Detail ?? "erreur inconnue")
        };
    }

    private async Task<decimal> ResolveAmountUsdAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        var planPrice = await dbContext.OrganizationSubscriptions
            .AsNoTracking()
            .Where(x => x.OrganizationId == organizationId && x.IsActive)
            .Select(x => x.SubscriptionPlan!.PublishCreditPriceUsd)
            .FirstOrDefaultAsync(cancellationToken);

        if (planPrice > 0)
            return planPrice;

        var deployFee = await dbContext.OrganizationSubscriptions
            .AsNoTracking()
            .Where(x => x.OrganizationId == organizationId && x.IsActive)
            .Select(x => x.SubscriptionPlan!.DeployFeeUsd)
            .FirstOrDefaultAsync(cancellationToken);

        if (deployFee > 0)
            return deployFee;

        return options.Value.DefaultAgentConsumableAmountUsd;
    }

    private string LogAndReturn(string productCode, string message, params object?[] args)
    {
        logger.LogInformation(message, args);
        return productCode;
    }

    private string? LogNull(string message, params object?[] args)
    {
        logger.LogWarning(message, args);
        return null;
    }

    private static string Truncate(string value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var trimmed = value.Trim();
        return trimmed.Length <= max ? trimmed : trimmed[..max] + "…";
    }
}

using System.Text.Json;
using AgenticFactory.Application;
using AgenticFactory.Domain;
using AgenticFactory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AgenticFactory.Infrastructure.Billing;

public sealed class SubscriptionBillingService(
    AgenticFactoryDbContext dbContext,
    IGisebsPayGatewayClient payGatewayClient,
    IOptions<GisebsApiPayGatewayOptions> options) : ISubscriptionBillingService
{
    private static readonly JsonSerializerOptions MetadataJsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<(BillingCheckoutResult? Result, string? Error)> StartCheckoutAsync(
        Guid organizationId,
        BillingCheckoutRequest request,
        CancellationToken cancellationToken)
    {
        if (!payGatewayClient.IsConfigured)
        {
            return (null, "Le paiement en ligne n'est pas encore configuré. Contactez support@agentia.io pour une mise à niveau.");
        }

        var plan = await dbContext.SubscriptionPlans
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.SubscriptionPlanId, cancellationToken);

        if (plan is null)
            return (null, "Plan d'abonnement introuvable.");

        if (plan.MonthlyPriceUsd <= 0)
            return (null, "Ce plan ne nécessite pas de paiement en ligne.");

        var current = await dbContext.OrganizationSubscriptions
            .AsNoTracking()
            .Include(x => x.SubscriptionPlan)
            .FirstOrDefaultAsync(x => x.OrganizationId == organizationId && x.IsActive, cancellationToken);

        if (current?.SubscriptionPlanId == plan.Id)
            return (null, "Vous êtes déjà abonné à ce plan.");

        var checkout = new SubscriptionCheckout
        {
            OrganizationId = organizationId,
            SubscriptionPlanId = plan.Id,
            Kind = CheckoutKind.Subscription,
            Quantity = 1,
            PaymentCode = $"TMP-{Guid.NewGuid():N}"[..24],
            CustomerEmail = request.CustomerEmail,
            Status = SubscriptionCheckoutStatus.Pending
        };
        dbContext.SubscriptionCheckouts.Add(checkout);
        await dbContext.SaveChangesAsync(cancellationToken);

        var config = options.Value;
        var productCode = config.BuildProductCode(plan.Name);
        var customerCode = BuildCustomerCode(organizationId);
        var metadataJson = JsonSerializer.Serialize(new
        {
            organizationId,
            subscriptionPlanId = plan.Id,
            planName = plan.Name
        }, MetadataJsonOptions);

        var gatewayRequest = new GisebsCheckoutSessionRequest(
            customerCode,
            request.CustomerEmail,
            request.CustomerName,
            organizationId.ToString(),
            productCode,
            config.DefaultPlanCode,
            AppendQuery(request.SuccessUrl, "checkoutId", checkout.Id.ToString()),
            request.CancelUrl,
            metadataJson);

        BillingCheckoutResult checkoutResult;
        try
        {
            checkoutResult = await payGatewayClient.CreateCheckoutSessionAsync(gatewayRequest, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            dbContext.SubscriptionCheckouts.Remove(checkout);
            await dbContext.SaveChangesAsync(cancellationToken);
            return (null, ex.Message);
        }

        checkout.PaymentCode = checkoutResult.PaymentCode;
        checkout.UpdatedAtUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        return (new BillingCheckoutResult(
            checkout.Id,
            checkoutResult.PaymentCode,
            checkoutResult.CheckoutUrl,
            checkoutResult.SessionId,
            checkoutResult.Status), null);
    }

    public async Task<(BillingCheckoutResult? Result, string? Error)> StartConsumableCheckoutAsync(
        Guid organizationId,
        ConsumableCheckoutRequest request,
        CancellationToken cancellationToken)
    {
        if (!payGatewayClient.IsConfigured)
        {
            return (null, "Le paiement en ligne n'est pas encore configuré. Contactez support@agentia.io.");
        }

        if (request.Kind is not (CheckoutKind.PublishCredits or CheckoutKind.RunPack))
            return (null, "Type d'achat consommable invalide.");

        var quantity = Math.Max(1, request.Quantity);

        var subscription = await dbContext.OrganizationSubscriptions
            .Include(x => x.SubscriptionPlan)
            .FirstOrDefaultAsync(x => x.OrganizationId == organizationId && x.IsActive, cancellationToken);

        if (subscription?.SubscriptionPlan is null)
            return (null, "Abonnement actif requis avant d'acheter des crédits.");

        var plan = subscription.SubscriptionPlan;
        var config = options.Value;
        string productCode;
        decimal unitPrice;
        int packSize;

        if (request.Kind == CheckoutKind.PublishCredits)
        {
            productCode = config.PublishCreditProductCode;
            unitPrice = plan.PublishCreditPriceUsd > 0 ? plan.PublishCreditPriceUsd : 49m;
            packSize = Math.Max(1, plan.PublishCreditPackSize);
        }
        else
        {
            productCode = config.RunPackProductCode;
            unitPrice = plan.RunPackPriceUsd > 0 ? plan.RunPackPriceUsd : 29m;
            packSize = Math.Max(1, plan.RunPackSize);
        }

        _ = unitPrice;
        _ = packSize;

        var checkout = new SubscriptionCheckout
        {
            OrganizationId = organizationId,
            SubscriptionPlanId = plan.Id,
            Kind = request.Kind,
            Quantity = quantity,
            PaymentCode = $"TMP-{Guid.NewGuid():N}"[..24],
            CustomerEmail = request.CustomerEmail,
            Status = SubscriptionCheckoutStatus.Pending
        };
        dbContext.SubscriptionCheckouts.Add(checkout);
        await dbContext.SaveChangesAsync(cancellationToken);

        var metadataJson = JsonSerializer.Serialize(new
        {
            organizationId,
            subscriptionPlanId = plan.Id,
            kind = request.Kind.ToString(),
            quantity,
            packSize
        }, MetadataJsonOptions);

        var gatewayRequest = new GisebsCheckoutSessionRequest(
            BuildCustomerCode(organizationId),
            request.CustomerEmail,
            request.CustomerName,
            organizationId.ToString(),
            productCode,
            config.DefaultPlanCode,
            AppendQuery(request.SuccessUrl, "checkoutId", checkout.Id.ToString()),
            request.CancelUrl,
            metadataJson);

        BillingCheckoutResult checkoutResult;
        try
        {
            checkoutResult = await payGatewayClient.CreateCheckoutSessionAsync(gatewayRequest, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            dbContext.SubscriptionCheckouts.Remove(checkout);
            await dbContext.SaveChangesAsync(cancellationToken);
            return (null, ex.Message);
        }

        checkout.PaymentCode = checkoutResult.PaymentCode;
        checkout.UpdatedAtUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        return (new BillingCheckoutResult(
            checkout.Id,
            checkoutResult.PaymentCode,
            checkoutResult.CheckoutUrl,
            checkoutResult.SessionId,
            checkoutResult.Status), null);
    }

    public async Task<(BillingConfirmResult? Result, string? Error)> ConfirmPaymentAsync(
        Guid organizationId,
        string? paymentCode,
        Guid? checkoutId,
        CancellationToken cancellationToken)
    {
        SubscriptionCheckout? checkout;
        if (checkoutId is Guid id && id != Guid.Empty)
        {
            checkout = await dbContext.SubscriptionCheckouts
                .Include(x => x.SubscriptionPlan)
                .FirstOrDefaultAsync(x => x.OrganizationId == organizationId && x.Id == id, cancellationToken);
        }
        else if (!string.IsNullOrWhiteSpace(paymentCode))
        {
            checkout = await dbContext.SubscriptionCheckouts
                .Include(x => x.SubscriptionPlan)
                .FirstOrDefaultAsync(
                    x => x.OrganizationId == organizationId && x.PaymentCode == paymentCode,
                    cancellationToken);
        }
        else
        {
            return (null, "Identifiant de paiement manquant.");
        }

        if (checkout is null)
            return (null, "Session de paiement introuvable pour votre organisation.");

        paymentCode = checkout.PaymentCode;

        if (checkout.Status == SubscriptionCheckoutStatus.Completed && checkout.SubscriptionPlan is not null)
        {
            var doneMessage = checkout.Kind switch
            {
                CheckoutKind.PublishCredits => "Crédits de publication déjà crédités.",
                CheckoutKind.RunPack => "Pack de runs déjà crédité.",
                _ => "Abonnement déjà activé."
            };
            return (new BillingConfirmResult(true, checkout.SubscriptionPlan.Name, doneMessage), null);
        }

        if (!payGatewayClient.IsConfigured)
            return (null, "GisebsApiPayGateway n'est pas configuré.");

        GisebsPaymentStatus? payment = null;
        const int maxAttempts = 5;
        const int retryDelayMs = 2000;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            payment = await payGatewayClient.GetPaymentStatusAsync(paymentCode, cancellationToken);
            if (payment is not null && GisebsPayGatewayClient.IsPaymentSuccessful(payment))
                break;

            if (attempt < maxAttempts)
                await Task.Delay(retryDelayMs, cancellationToken);
        }

        if (payment is null)
            return (null, "Paiement introuvable ou en attente de confirmation. Réessayez dans quelques instants.");

        if (!string.Equals(payment.CustomerCode, BuildCustomerCode(organizationId), StringComparison.OrdinalIgnoreCase))
            return (null, "Ce paiement ne correspond pas à votre organisation.");

        if (!GisebsPayGatewayClient.IsPaymentSuccessful(payment))
        {
            checkout.Status = SubscriptionCheckoutStatus.Failed;
            checkout.UpdatedAtUtc = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
            return (null, "Le paiement n'a pas été confirmé. Réessayez ou contactez le support.");
        }

        var plan = checkout.SubscriptionPlan
            ?? await dbContext.SubscriptionPlans.FirstAsync(x => x.Id == checkout.SubscriptionPlanId, cancellationToken);

        if (checkout.Kind == CheckoutKind.Subscription)
        {
            await ActivateSubscriptionAsync(organizationId, plan, paymentCode, payment.PaidAt, cancellationToken);
        }
        else
        {
            await CreditConsumablesAsync(organizationId, plan, checkout, cancellationToken);
        }

        checkout.Status = SubscriptionCheckoutStatus.Completed;
        checkout.PaidAtUtc = payment.PaidAt ?? DateTime.UtcNow;
        checkout.UpdatedAtUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        var message = checkout.Kind switch
        {
            CheckoutKind.PublishCredits => $"{checkout.Quantity * Math.Max(1, plan.PublishCreditPackSize)} crédit(s) de publication ajouté(s).",
            CheckoutKind.RunPack => $"{checkout.Quantity * Math.Max(1, plan.RunPackSize):N0} runs ajoutés à votre solde.",
            _ => "Votre abonnement a été activé avec succès."
        };

        return (new BillingConfirmResult(true, plan.Name, message), null);
    }

    private static string AppendQuery(string url, string key, string value)
    {
        var separator = url.Contains('?', StringComparison.Ordinal) ? '&' : '?';
        return $"{url}{separator}{Uri.EscapeDataString(key)}={Uri.EscapeDataString(value)}";
    }

    private async Task ActivateSubscriptionAsync(
        Guid organizationId,
        SubscriptionPlan plan,
        string paymentCode,
        DateTime? paidAt,
        CancellationToken cancellationToken)
    {
        var now = paidAt ?? DateTime.UtcNow;
        var activeSubscriptions = await dbContext.OrganizationSubscriptions
            .Where(x => x.OrganizationId == organizationId && x.IsActive)
            .ToListAsync(cancellationToken);

        var preservedCredits = activeSubscriptions.Sum(x => x.PublishCredits);
        var preservedRuns = activeSubscriptions.Sum(x => x.ConsumableRunsBalance);

        foreach (var existing in activeSubscriptions)
        {
            existing.IsActive = false;
            existing.UpdatedAtUtc = DateTime.UtcNow;
        }

        dbContext.OrganizationSubscriptions.Add(new OrganizationSubscription
        {
            OrganizationId = organizationId,
            SubscriptionPlanId = plan.Id,
            IsActive = true,
            UsedRunsThisMonth = 0,
            PeriodStartUtc = now.Date,
            PeriodEndUtc = now.Date.AddMonths(1),
            PayGatewayPaymentCode = paymentCode,
            PublishCredits = preservedCredits,
            ConsumableRunsBalance = preservedRuns
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task CreditConsumablesAsync(
        Guid organizationId,
        SubscriptionPlan plan,
        SubscriptionCheckout checkout,
        CancellationToken cancellationToken)
    {
        var subscription = await dbContext.OrganizationSubscriptions
            .FirstOrDefaultAsync(x => x.OrganizationId == organizationId && x.IsActive, cancellationToken)
            ?? throw new InvalidOperationException("Abonnement actif introuvable.");

        var quantity = Math.Max(1, checkout.Quantity);
        if (checkout.Kind == CheckoutKind.PublishCredits)
        {
            subscription.PublishCredits += quantity * Math.Max(1, plan.PublishCreditPackSize);
        }
        else if (checkout.Kind == CheckoutKind.RunPack)
        {
            subscription.ConsumableRunsBalance += quantity * Math.Max(1, plan.RunPackSize);
        }

        subscription.UpdatedAtUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    internal static string BuildCustomerCode(Guid organizationId) => $"AO-{organizationId:N}";
}

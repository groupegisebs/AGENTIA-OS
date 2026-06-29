using AgenticFactory.Application;
using AgenticFactory.Domain;
using AgenticFactory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AgenticFactory.Infrastructure.Billing;

public sealed class PublishEligibilityService(
    AgenticFactoryDbContext dbContext,
    IOptions<BillingOptions> billingOptions) : IPublishEligibilityService
{
    public async Task<PublishEligibilityResult> EvaluateAsync(
        Guid organizationId,
        Guid? agentIdToDeploy,
        CancellationToken cancellationToken)
    {
        if (billingOptions.Value.SkipPublishPaymentGate)
        {
            return PublishEligibilityResult.Eligible();
        }

        var subscription = await dbContext.OrganizationSubscriptions
            .Include(x => x.SubscriptionPlan)
            .FirstOrDefaultAsync(x => x.OrganizationId == organizationId && x.IsActive, cancellationToken);

        if (subscription?.SubscriptionPlan is null)
        {
            return PublishEligibilityResult.Blocked(
                "no_subscription",
                "Aucun abonnement actif.",
                "Payer l'abonnement",
                "subscription",
                requiredAmountUsd: null,
                subscriptionPlanId: null,
                publishCreditsBalance: 0,
                deployedAgents: 0,
                maxAgents: 0);
        }

        var plan = subscription.SubscriptionPlan;
        var deployedAgents = await dbContext.Agents.CountAsync(
            x => x.OrganizationId == organizationId && x.Status == AgentStatus.Active,
            cancellationToken);

        var isRedeploy = agentIdToDeploy is Guid agentId
            && await dbContext.Agents.AnyAsync(
                x => x.Id == agentId && x.OrganizationId == organizationId && x.Status == AgentStatus.Active,
                cancellationToken);

        var subscriptionPaid = plan.MonthlyPriceUsd <= 0
            || !string.IsNullOrWhiteSpace(subscription.PayGatewayPaymentCode);

        if (!subscriptionPaid)
        {
            return PublishEligibilityResult.Blocked(
                "subscription_payment_required",
                $"Un abonnement {plan.Name} payant ({plan.MonthlyPriceUsd:0} $/mois) est requis pour publier un agent.",
                "Payer l'abonnement",
                "subscription",
                plan.MonthlyPriceUsd,
                plan.Id,
                subscription.PublishCredits,
                deployedAgents,
                plan.MaxAgents);
        }

        if (!isRedeploy && deployedAgents >= plan.MaxAgents)
        {
            if (plan.PublishModel == PublishModel.ConsumableExtra)
            {
                if (subscription.PublishCredits < 1)
                {
                    var packPrice = plan.PublishCreditPriceUsd > 0
                        ? plan.PublishCreditPriceUsd
                        : plan.DeployFeeUsd > 0 ? plan.DeployFeeUsd : 49m;

                    return PublishEligibilityResult.Blocked(
                        "publish_credits_required",
                        $"Quota de {plan.MaxAgents} agents atteint. Achetez un crédit de publication pour rendre cet agent public.",
                        "Acheter des crédits de publication",
                        "publish_credits",
                        packPrice,
                        plan.Id,
                        subscription.PublishCredits,
                        deployedAgents,
                        plan.MaxAgents);
                }
            }
            else
            {
                return PublishEligibilityResult.Blocked(
                    "agent_quota_exceeded",
                    $"Quota de {plan.MaxAgents} agents déployés atteint. Passez à un plan supérieur.",
                    "Mettre à niveau l'abonnement",
                    "subscription",
                    plan.MonthlyPriceUsd,
                    plan.Id,
                    subscription.PublishCredits,
                    deployedAgents,
                    plan.MaxAgents);
            }
        }

        return PublishEligibilityResult.Eligible(
            plan.Name,
            plan.MonthlyPriceUsd,
            subscription.PublishCredits,
            deployedAgents,
            plan.MaxAgents,
            consumesPublishCredit: !isRedeploy && deployedAgents >= plan.MaxAgents && plan.PublishModel == PublishModel.ConsumableExtra);
    }
}

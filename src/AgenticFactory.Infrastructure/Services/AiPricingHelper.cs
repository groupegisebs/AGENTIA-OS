using System.Globalization;
using AgenticFactory.Domain;
using Microsoft.Extensions.Configuration;

namespace AgenticFactory.Infrastructure.Services;

public static class AiPricingHelper
{
    public sealed record TokenCostEstimate(int PromptTokens, int CompletionTokens, decimal EstimatedCostUsd);

    public static TokenCostEstimate EstimateFromPrompt(string prompt, IConfiguration configuration)
    {
        var promptTokens = Math.Max(30, prompt.Length / 4);
        var completionTokens = Math.Max(120, promptTokens / 2);
        var cost = EstimateCostUsd(promptTokens, completionTokens, configuration);
        return new TokenCostEstimate(promptTokens, completionTokens, cost);
    }

    public static decimal EstimateCostUsd(int promptTokens, int completionTokens, IConfiguration configuration)
    {
        var promptRate = ParseDecimal(configuration["AI:Pricing:PromptPer1kUsd"], 0.00015m);
        var completionRate = ParseDecimal(configuration["AI:Pricing:CompletionPer1kUsd"], 0.0006m);
        var promptCost = (promptTokens / 1000m) * promptRate;
        var completionCost = (completionTokens / 1000m) * completionRate;
        return Math.Round(promptCost + completionCost, 6);
    }

    public static void ResetBillingPeriodIfNeeded(OrganizationSubscription subscription)
    {
        if (DateTime.UtcNow < subscription.PeriodEndUtc)
            return;

        subscription.PeriodStartUtc = subscription.PeriodEndUtc;
        subscription.PeriodEndUtc = subscription.PeriodStartUtc.AddMonths(1);
        subscription.UsedRunsThisMonth = 0;
    }

    private static decimal ParseDecimal(string? value, decimal fallback) =>
        decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;
}

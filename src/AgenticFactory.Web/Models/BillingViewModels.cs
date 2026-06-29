namespace AgenticFactory.Web.Models;

public sealed class SubscriptionsIndexViewModel
{
    public bool HasSubscription { get; set; }
    public string PlanName { get; set; } = "Aucun";
    public int MaxAgents { get; set; }
    public int MaxRunsPerMonth { get; set; }
    public decimal MonthlyPriceUsd { get; set; }
    public int UsedRunsThisMonth { get; set; }
    public int CurrentAgents { get; set; }
    public DateTime? PeriodStartUtc { get; set; }
    public DateTime? PeriodEndUtc { get; set; }
    public List<SubscriptionPlanItem> AvailablePlans { get; set; } = [];
    public int PublishCredits { get; set; }
    public int ConsumableRunsBalance { get; set; }
    public bool SubscriptionPaid { get; set; }
    public string? PublishModel { get; set; }
    public bool ApiAvailable { get; set; } = true;
    public bool PaymentConfigured { get; set; }
    public string? PaymentMessage { get; set; }
    public string? FlashMessage { get; set; }
    public string? FlashError { get; set; }
    public string UserDisplayName { get; set; } = "Utilisateur";
    public string UserRole { get; set; } = "Membre";
}

public sealed record SubscriptionPlanItem(
    Guid Id,
    string Name,
    int MaxAgents,
    int MaxRunsPerMonth,
    decimal MonthlyPriceUsd,
    decimal BlueprintCreationFeeUsd,
    decimal DeployFeeUsd,
    bool IsCurrent,
    string? PublishModel = null,
    decimal PublishCreditPriceUsd = 0,
    decimal RunPackPriceUsd = 0);

public sealed class BillingIndexViewModel
{
    public string OrganizationName { get; set; } = "Organisation";
    public string PlanName { get; set; } = "Aucun";
    public decimal MonthlyPriceUsd { get; set; }
    public DateTime? PeriodStartUtc { get; set; }
    public DateTime? PeriodEndUtc { get; set; }
    public bool HasPaymentMethod { get; set; }
    public string? PaymentMethodLabel { get; set; }
    public List<InvoiceItem> Invoices { get; set; } = [];
    public bool ApiAvailable { get; set; } = true;
    public string UserDisplayName { get; set; } = "Utilisateur";
    public string UserRole { get; set; } = "Membre";
}

public sealed record InvoiceItem(
    string Number,
    DateTime DateUtc,
    decimal AmountUsd,
    string Status);

public sealed class UsageIndexViewModel
{
    public int RunsThisMonth { get; set; }
    public long TokensThisMonth { get; set; }
    public decimal CostThisMonth { get; set; }
    public decimal AiRunCostThisMonth { get; set; }
    public decimal CreationCostThisMonth { get; set; }
    public decimal DeployCostThisMonth { get; set; }
    public int BlueprintsThisMonth { get; set; }
    public int DeploymentsThisMonth { get; set; }
    public int FailedThisMonth { get; set; }
    public int UsedRunsQuota { get; set; }
    public int MaxRunsPerMonth { get; set; }
    public string PeriodLabel { get; set; } = "";
    public List<int> TokenSeries { get; set; } = [];
    public List<int> RunSeries { get; set; } = [];
    public int TotalRuns { get; set; }
    public long TotalTokens { get; set; }
    public decimal TotalCost { get; set; }
    public decimal AiRunCostTotal { get; set; }
    public decimal CreationCostTotal { get; set; }
    public decimal DeployCostTotal { get; set; }
    public bool ApiAvailable { get; set; } = true;
    public string UserDisplayName { get; set; } = "Utilisateur";
    public string UserRole { get; set; } = "Membre";
}

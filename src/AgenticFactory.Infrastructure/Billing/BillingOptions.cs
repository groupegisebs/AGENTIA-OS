namespace AgenticFactory.Infrastructure.Billing;

public sealed class BillingOptions
{
    public const string SectionName = "Billing";

    /// <summary>Contourne la porte de paiement à la publication (tests / dev explicite uniquement).</summary>
    public bool SkipPublishPaymentGate { get; set; }
}

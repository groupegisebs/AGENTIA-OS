namespace AgenticFactory.Infrastructure.Billing;

public sealed class PublishPaymentRequiredException(PublishEligibilityResult eligibility) : InvalidOperationException(eligibility.MessageFr)
{
    public PublishEligibilityResult Eligibility { get; } = eligibility;
}

namespace AgenticFactory.Infrastructure.Billing;

public sealed class GisebsApiPayGatewayOptions
{
    public const string SectionName = "GisebsApiPayGateway";

    public const string DefaultProductionBaseUrl = "https://gisebsapipaygateway.gisebs.com";

    public string BaseUrl { get; set; } = string.Empty;
    public string AppCode { get; set; } = "AGENTIAOS";
    public string ApiKey { get; set; } = string.Empty;
    public string WebhookSecret { get; set; } = string.Empty;
    public string DefaultPlanCode { get; set; } = "MONTHLY";
    public string ConsumablePlanCode { get; set; } = "ONE-TIME";
    public string ProductCodePrefix { get; set; } = "AGENTIA";
    public decimal DefaultAgentConsumableAmountUsd { get; set; } = 49m;
    public bool RequireHttps { get; set; } = true;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(BaseUrl)
        && !string.IsNullOrWhiteSpace(AppCode)
        && !string.IsNullOrWhiteSpace(ApiKey);

    public Uri GetBaseUri()
    {
        if (!Uri.TryCreate(BaseUrl.TrimEnd('/'), UriKind.Absolute, out var uri))
            throw new InvalidOperationException("GisebsApiPayGateway:BaseUrl invalide.");

        return uri;
    }

    public void EnsureSecureEndpoint(bool isDevelopment)
    {
        if (!RequireHttps)
            return;

        var uri = GetBaseUri();
        var isLocal = uri.Host is "localhost" or "127.0.0.1";
        if (isDevelopment || isLocal || uri.Scheme == Uri.UriSchemeHttps)
            return;

        throw new InvalidOperationException(
            "GisebsApiPayGateway:BaseUrl doit utiliser HTTPS (ou définir RequireHttps=false en développement).");
    }

    public string BuildProductCode(string planName) =>
        $"{ProductCodePrefix}-{planName.Trim().Replace(' ', '-').ToUpperInvariant()}";

    public string BuildAgentProductCode(Guid agentId) =>
        $"{ProductCodePrefix}-AGENT-{agentId.ToString("N")[..8].ToUpperInvariant()}";

    public string PublishCreditProductCode => $"{ProductCodePrefix}-PUBLISH-CREDIT";
    public string RunPackProductCode => $"{ProductCodePrefix}-RUN-PACK";
}

using AgenticFactory.Domain;

namespace AgenticFactory.Application;

public sealed record ChatMessageRequest(string Message, Guid? ExistingAgentId = null, string? CapabilityGraphJson = null);
public sealed record BlueprintResponse(
    string BlueprintJson,
    string Summary,
    bool IsValid,
    string ValidationNotes,
    decimal EstimatedCostUsd = 0,
    int PromptTokens = 0,
    int CompletionTokens = 0);
public sealed record BlueprintCreatedResponse(
    Guid Id,
    Guid AgentId,
    string PromptSummary,
    string BlueprintJson,
    string Status,
    string ValidationNotes,
    decimal CreationCostUsd = 0,
    int PromptTokens = 0,
    int CompletionTokens = 0);
public sealed record DeployAgentRequest(Guid AgentId, Guid BlueprintId, string Environment);
public sealed record DeployAgentResponse(
    Guid AgentId,
    Guid AgentVersionId,
    Guid DeploymentId,
    string EndpointSlug,
    string PlainApiKey,
    decimal DeployFeeUsd = 0,
    int CurrentAgents = 0,
    int MaxAgents = 0);
public sealed record InvokeAgentRequest(Dictionary<string, object?> Input);
public sealed record InvokeAgentResponse(Guid RunId, string Status, Dictionary<string, object?> Output, int PromptTokens, int CompletionTokens, decimal EstimatedCostUsd);
public sealed record ModelGenerationRequest(Guid OrganizationId, string Prompt, string? SystemPrompt);
public sealed record ModelGenerationResult(
    string Output,
    int PromptTokens,
    int CompletionTokens,
    decimal EstimatedCostUsd,
    string Provider,
    bool UsedFallback);

public interface IBlueprintGenerator
{
    Task<BlueprintResponse> GenerateAsync(Guid organizationId, string message, CancellationToken cancellationToken);
    Task<BlueprintResponse> ValidateAsync(string blueprintJson, CancellationToken cancellationToken);
}

public interface IAgentCreationService
{
    Task<AgentBlueprint> CreateBlueprintFromChatAsync(Guid organizationId, ChatMessageRequest request, CancellationToken cancellationToken);
    Task<BlueprintResponse> ValidateBlueprintAsync(string blueprintJson, CancellationToken cancellationToken);
}

public interface IAgentDeploymentService
{
    Task<DeployAgentResponse> DeployAsync(Guid organizationId, DeployAgentRequest request, CancellationToken cancellationToken);
}

public interface IAgentInvocationService
{
    Task<InvokeAgentResponse> InvokeAsync(Guid organizationId, string endpointSlug, string apiKey, InvokeAgentRequest request, CancellationToken cancellationToken);
}

public interface ICurrentTenantService
{
    Guid OrganizationId { get; }
}

public interface IJwtTokenService
{
    string CreateToken(Guid userId, Guid organizationId, string email, IEnumerable<string> roles);
}

public interface IAgentRuntime
{
    Task TickAsync(CancellationToken cancellationToken);
}

public interface IAgentExecutor
{
    Task<InvokeAgentResponse> ExecuteAsync(Guid organizationId, Agent agent, AgentVersion version, Dictionary<string, object?> input, CancellationToken cancellationToken);
}

public interface IAgentToolExecutor
{
    Task<Dictionary<string, object?>> ExecuteToolsAsync(Guid organizationId, Agent agent, Dictionary<string, object?> input, CancellationToken cancellationToken);
}

public interface IAgentMemoryService
{
    Task RememberAsync(Guid runId, string data, CancellationToken cancellationToken);
    Task RememberEntryAsync(Guid organizationId, Guid agentId, Guid runId, string key, string value, bool isShortTerm, CancellationToken cancellationToken);
    Task<IReadOnlyList<AgentMemorySnapshot>> GetRecentAsync(Guid organizationId, Guid agentId, int take, CancellationToken cancellationToken);
}

public sealed record AgentMemorySnapshot(Guid Id, string Key, string Value, bool IsShortTerm, DateTime CreatedAtUtc);

public interface IAgentModelProvider
{
    Task<ModelGenerationResult> GenerateAsync(ModelGenerationRequest request, CancellationToken cancellationToken);
}

public interface IGraphRuntimeEngine
{
    bool CanExecute(string? definitionJson);
    Task<GraphExecutionResult> ExecuteAsync(
        Guid organizationId,
        Guid agentId,
        Guid runId,
        string definitionJson,
        Dictionary<string, object?> input,
        CancellationToken cancellationToken);
    Task<GraphExecutionResult> ExecuteDryAsync(
        Guid organizationId,
        string definitionJson,
        Dictionary<string, object?> input,
        CancellationToken cancellationToken);
}

public interface ISecretStore
{
    Task<Guid> UpsertAsync(Guid organizationId, string name, string plaintextValue, string? provider, CancellationToken cancellationToken);
    Task<string?> GetPlaintextAsync(Guid organizationId, Guid secretId, CancellationToken cancellationToken);
    Task<string?> ResolveRefAsync(Guid organizationId, string? secretRefOrPlain, CancellationToken cancellationToken);
    Task<IReadOnlyList<SecretListItem>> ListAsync(Guid organizationId, CancellationToken cancellationToken);
}

public sealed record SecretListItem(Guid Id, string Name, string? Provider, DateTime UpdatedAtUtc);

public interface IKnowledgeService
{
    Task<Guid> CreateBaseAsync(Guid organizationId, Guid agentId, string name, CancellationToken cancellationToken);
    Task<Guid> IngestDocumentAsync(Guid organizationId, Guid knowledgeBaseId, string title, string content, CancellationToken cancellationToken);
    Task<IReadOnlyList<KnowledgeHit>> SearchAsync(Guid organizationId, Guid agentId, string query, int topK, CancellationToken cancellationToken);
}

public sealed record KnowledgeHit(Guid DocumentId, string Title, string Snippet, double Score);

public interface IMarketplaceService
{
    Task<MarketplaceListingDto> PublishAsync(Guid organizationId, Guid agentId, MarketplacePublishRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<MarketplaceListingDto>> ListPublishedAsync(CancellationToken cancellationToken);
    Task<MarketplaceListingDto?> GetAsync(Guid listingId, CancellationToken cancellationToken);
    Task RollbackVersionAsync(Guid organizationId, Guid agentId, int versionNumber, CancellationToken cancellationToken);
}

public sealed record MarketplacePublishRequest(
    decimal PriceUsd,
    string License,
    string Description,
    string Category,
    string? DocumentationUrl);

public sealed record MarketplaceListingDto(
    Guid Id,
    Guid AgentId,
    string AgentName,
    string Author,
    decimal PriceUsd,
    string License,
    string Description,
    string Category,
    string Status,
    int VersionNumber,
    DateTime UpdatedAtUtc,
    string? PayGatewayProductCode);

public interface IAgentOptimizationService
{
    Task<IReadOnlyList<AgentOptimizationSuggestion>> AnalyzeAsync(Guid organizationId, Guid agentId, CancellationToken cancellationToken);
}

public sealed record AgentOptimizationSuggestion(
    string Code,
    string Title,
    string Detail,
    string Impact,
    double EstimatedSavingsPercent);

public interface IObservatoryService
{
    Task<ObservatorySnapshot> GetSnapshotAsync(Guid organizationId, CancellationToken cancellationToken);
}

public sealed record ObservatorySnapshot(
    int ActiveAgents,
    long DocumentsProcessed,
    decimal AiCostUsd,
    double AvgDurationSeconds,
    double GlobalHealthPercent,
    IReadOnlyList<ObservatoryAgentRow> Agents);

public sealed record ObservatoryAgentRow(
    Guid AgentId,
    string Name,
    string Status,
    int Runs24h,
    double SuccessRate,
    decimal Cost24hUsd);

public sealed record BillingCheckoutRequest(
    Guid SubscriptionPlanId,
    string CustomerEmail,
    string? CustomerName,
    string SuccessUrl,
    string CancelUrl);

public sealed record BillingCheckoutResult(
    Guid CheckoutId,
    string PaymentCode,
    string CheckoutUrl,
    string SessionId,
    string Status);

public sealed record BillingConfirmResult(
    bool Activated,
    string PlanName,
    string? Message);

public sealed record GisebsCheckoutSessionRequest(
    string CustomerCode,
    string Email,
    string? FullName,
    string? ExternalUserId,
    string ProductCode,
    string PlanCode,
    string SuccessUrl,
    string CancelUrl,
    string? MetadataJson);

public sealed record GisebsCatalogItemRequest(
    string ProductCode,
    string ProductName,
    string? Description,
    string PlanCode,
    string PlanName,
    decimal Amount,
    string Currency,
    bool SyncToStripe = true);

public enum GisebsCatalogItemOutcome
{
    Created,
    AlreadyExists,
    NotConfigured,
    Failed
}

public sealed record GisebsCatalogItemResult(
    GisebsCatalogItemOutcome Outcome,
    string? ProductCode = null,
    string? Detail = null);

public interface IGisebsPayGatewayClient
{
    bool IsConfigured { get; }
    Task<BillingCheckoutResult> CreateCheckoutSessionAsync(GisebsCheckoutSessionRequest request, CancellationToken cancellationToken);
    Task<GisebsPaymentStatus?> GetPaymentStatusAsync(string paymentCode, CancellationToken cancellationToken);
    Task<GisebsCatalogItemResult> TryCreateCatalogItemAsync(GisebsCatalogItemRequest request, CancellationToken cancellationToken);
}

public interface IAgentPayGatewayProductService
{
    Task<string?> TryEnsureAgentProductAsync(Agent agent, Guid organizationId, CancellationToken cancellationToken);
}

public sealed record GisebsPaymentStatus(
    string PaymentCode,
    string Status,
    string CustomerCode,
    string ProductCode,
    string PlanCode,
    DateTime? PaidAt);

public interface ISubscriptionBillingService
{
    Task<(BillingCheckoutResult? Result, string? Error)> StartCheckoutAsync(
        Guid organizationId,
        BillingCheckoutRequest request,
        CancellationToken cancellationToken);

    Task<(BillingCheckoutResult? Result, string? Error)> StartConsumableCheckoutAsync(
        Guid organizationId,
        ConsumableCheckoutRequest request,
        CancellationToken cancellationToken);

    Task<(BillingConfirmResult? Result, string? Error)> ConfirmPaymentAsync(
        Guid organizationId,
        string? paymentCode,
        Guid? checkoutId,
        CancellationToken cancellationToken);
}

public sealed record ConsumableCheckoutRequest(
    CheckoutKind Kind,
    string CustomerEmail,
    string? CustomerName,
    string SuccessUrl,
    string CancelUrl,
    int Quantity = 1);

public sealed record PublishEligibilityResult(
    bool CanPublish,
    string? BlockReason,
    string MessageFr,
    string? CtaLabelFr,
    string? CheckoutAction,
    decimal? RequiredAmountUsd,
    Guid? SubscriptionPlanId,
    int PublishCreditsBalance,
    int DeployedAgents,
    int MaxAgents,
    bool ConsumesPublishCredit,
    string? PlanName = null,
    decimal MonthlyPriceUsd = 0)
{
    public static PublishEligibilityResult Eligible(
        string? planName = null,
        decimal monthlyPriceUsd = 0,
        int publishCredits = 0,
        int deployedAgents = 0,
        int maxAgents = 0,
        bool consumesPublishCredit = false) =>
        new(true, null, "Publication autorisée.", null, null, null, null,
            publishCredits, deployedAgents, maxAgents, consumesPublishCredit, planName, monthlyPriceUsd);

    public static PublishEligibilityResult Blocked(
        string blockReason,
        string messageFr,
        string ctaLabelFr,
        string checkoutAction,
        decimal? requiredAmountUsd,
        Guid? subscriptionPlanId,
        int publishCreditsBalance,
        int deployedAgents,
        int maxAgents) =>
        new(false, blockReason, messageFr, ctaLabelFr, checkoutAction, requiredAmountUsd, subscriptionPlanId,
            publishCreditsBalance, deployedAgents, maxAgents, false);
}

public interface IPublishEligibilityService
{
    Task<PublishEligibilityResult> EvaluateAsync(Guid organizationId, Guid? agentIdToDeploy, CancellationToken cancellationToken);
}

using System.ComponentModel.DataAnnotations;

namespace AgenticFactory.Domain;

public interface ITenantEntity
{
    Guid OrganizationId { get; set; }
}

public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public enum AgentStatus
{
    Draft = 1,
    Active = 2,
    Disabled = 3
}

public enum BlueprintStatus
{
    Proposed = 1,
    Validated = 2,
    Rejected = 3
}

public enum DeploymentStatus
{
    Pending = 1,
    Active = 2,
    Failed = 3
}

public enum RunStatus
{
    Queued = 1,
    Running = 2,
    Completed = 3,
    Failed = 4
}

public enum TriggerType
{
    Interval = 1,
    Scheduled = 2,
    Webhook = 3
}

public enum DomainRequestStatus
{
    Pending = 1,
    InReview = 2,
    Approved = 3,
    Rejected = 4
}

public enum SubscriptionCheckoutStatus
{
    Pending = 1,
    Completed = 2,
    Failed = 3,
    Cancelled = 4
}

public enum PublishModel
{
    /// <summary>Publication incluse dans l'abonnement mensuel (quota MaxAgents).</summary>
    SubscriptionIncluded = 1,
    /// <summary>Agents supplémentaires via crédits de publication consommables.</summary>
    ConsumableExtra = 2
}

public enum CheckoutKind
{
    Subscription = 1,
    PublishCredits = 2,
    RunPack = 3
}

public enum ExecutionProviderType
{
    InternalRuntime = 1,
    PowerAutomate = 2,
    LogicApps = 3,
    N8n = 4,
    Webhook = 5,
    RestApi = 6,
    PowerShell = 7,
    Python = 8,
    DockerJob = 9,
    AzureFunction = 10,
    WindowsScript = 11
}

public enum ExecutionMode
{
    Synchronous = 1,
    Asynchronous = 2,
    Scheduled = 3
}

public enum ErrorPolicy
{
    Fail = 1,
    Continue = 2,
    Compensate = 3
}

/// <summary>
/// Catalog entry for how agent actions can be executed (Power Automate, n8n, Windows Runtime, etc.).
/// </summary>
public class ActionExecutionProvider : BaseEntity
{
    [MaxLength(120)]
    public required string Name { get; set; }

    [MaxLength(500)]
    public required string Description { get; set; }

    [MaxLength(80)]
    public required string Category { get; set; }

    public ExecutionProviderType ProviderType { get; set; }
    public bool IsSystem { get; set; } = true;
    public bool SupportsParameters { get; set; }
    public bool SupportsMonitoring { get; set; }
    public bool SupportsRetry { get; set; }
    public bool SupportsRollback { get; set; }
    public bool SupportsScheduling { get; set; }

    [MaxLength(20)]
    public string Version { get; set; } = "1.0.0";

    [MaxLength(120)]
    public string Author { get; set; } = "Agentia";

    public bool IsEnabled { get; set; } = true;
}

/// <summary>
/// Agent action definition with execution provider binding (embedded in blueprint JSON or persisted per agent).
/// </summary>
public class AgentAction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? AgentId { get; set; }

    [MaxLength(120)]
    public required string ActionType { get; set; }

    [MaxLength(200)]
    public required string Label { get; set; }

    public Guid? ExecutionProviderId { get; set; }
    public ExecutionMode ExecutionMode { get; set; } = ExecutionMode.Synchronous;
    public string ExecutionConfigurationJson { get; set; } = "{}";
    public int TimeoutSeconds { get; set; } = 300;
    public string RetryPolicy { get; set; } = "{}";
    public ErrorPolicy ErrorPolicy { get; set; } = ErrorPolicy.Fail;
}

public class Organization : BaseEntity
{
    [MaxLength(150)]
    public required string Name { get; set; }

    [MaxLength(160)]
    public required string Slug { get; set; }

    public ICollection<ApplicationUser> Users { get; set; } = new List<ApplicationUser>();
    public ICollection<Agent> Agents { get; set; } = new List<Agent>();
}

public class ApplicationUser : BaseEntity, ITenantEntity
{
    public Guid OrganizationId { get; set; }
    public required string Email { get; set; }
    public required string DisplayName { get; set; }
    public required string IdentityUserId { get; set; }
    public Organization? Organization { get; set; }
}

public class Agent : BaseEntity, ITenantEntity
{
    public Guid OrganizationId { get; set; }
    public required string Name { get; set; }
    public required string Description { get; set; }
    public required string EndpointSlug { get; set; }
    public AgentStatus Status { get; set; } = AgentStatus.Draft;
    public Guid? ActiveVersionId { get; set; }

    public Organization? Organization { get; set; }
    public ICollection<AgentVersion> Versions { get; set; } = new List<AgentVersion>();
    public ICollection<AgentTrigger> Triggers { get; set; } = new List<AgentTrigger>();
}

public class AgentBlueprint : BaseEntity, ITenantEntity
{
    public Guid OrganizationId { get; set; }
    public Guid AgentId { get; set; }
    public required string PromptSummary { get; set; }
    public required string BlueprintJson { get; set; }
    public BlueprintStatus Status { get; set; } = BlueprintStatus.Proposed;
    public string ValidationNotes { get; set; } = string.Empty;
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public decimal CreationCostUsd { get; set; }
    public Agent? Agent { get; set; }
}

public class AgentVersion : BaseEntity, ITenantEntity
{
    public Guid OrganizationId { get; set; }
    public Guid AgentId { get; set; }
    public int VersionNumber { get; set; }
    public required string DefinitionJson { get; set; }
    public bool IsActive { get; set; }

    public Agent? Agent { get; set; }
    public ICollection<AgentDeployment> Deployments { get; set; } = new List<AgentDeployment>();
}

public class AgentDeployment : BaseEntity, ITenantEntity
{
    public Guid OrganizationId { get; set; }
    public Guid AgentId { get; set; }
    public Guid AgentVersionId { get; set; }
    public DeploymentStatus Status { get; set; } = DeploymentStatus.Pending;
    public required string Environment { get; set; }
    public required string ApiKeyHash { get; set; }
    public decimal DeployFeeUsd { get; set; }
    public DateTime? ActivatedAtUtc { get; set; }

    public Agent? Agent { get; set; }
    public AgentVersion? AgentVersion { get; set; }
}

public class AgentRun : BaseEntity, ITenantEntity
{
    public Guid OrganizationId { get; set; }
    public Guid AgentId { get; set; }
    public Guid AgentVersionId { get; set; }
    public RunStatus Status { get; set; } = RunStatus.Queued;
    public required string InputJson { get; set; }
    public string OutputJson { get; set; } = "{}";
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public decimal EstimatedCostUsd { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }

    public Agent? Agent { get; set; }
    public AgentVersion? AgentVersion { get; set; }
}

public class AgentTool : BaseEntity, ITenantEntity
{
    public Guid OrganizationId { get; set; }
    public Guid AgentId { get; set; }
    public required string Name { get; set; }
    public required string Type { get; set; }
    public required string ConfigurationJson { get; set; }
}

public class AgentTrigger : BaseEntity, ITenantEntity
{
    public Guid OrganizationId { get; set; }
    public Guid AgentId { get; set; }
    public TriggerType Type { get; set; }
    public required string CronOrInterval { get; set; }
    public bool IsEnabled { get; set; } = true;
    public DateTime? LastTriggeredAtUtc { get; set; }
    public Agent? Agent { get; set; }
}

public class SubscriptionPlan : BaseEntity
{
    [MaxLength(100)]
    public required string Name { get; set; }
    public int MaxAgents { get; set; }
    public int MaxRunsPerMonth { get; set; }
    public decimal MonthlyPriceUsd { get; set; }
    /// <summary>Frais forfaitaire par génération de blueprint (0 = inclus dans l'abonnement).</summary>
    public decimal BlueprintCreationFeeUsd { get; set; }
    /// <summary>Frais forfaitaire par déploiement (0 = inclus dans l'abonnement).</summary>
    public decimal DeployFeeUsd { get; set; }
    public PublishModel PublishModel { get; set; } = PublishModel.SubscriptionIncluded;
    /// <summary>Prix d'un pack de crédits de publication (1 crédit = 1 agent publié au-delà du quota).</summary>
    public decimal PublishCreditPriceUsd { get; set; }
    public int PublishCreditPackSize { get; set; } = 1;
    /// <summary>Prix d'un pack de runs consommables (au-delà du quota mensuel).</summary>
    public decimal RunPackPriceUsd { get; set; }
    public int RunPackSize { get; set; } = 1000;
}

public class OrganizationSubscription : BaseEntity, ITenantEntity
{
    public Guid OrganizationId { get; set; }
    public Guid SubscriptionPlanId { get; set; }
    public bool IsActive { get; set; } = true;
    public int UsedRunsThisMonth { get; set; }
    public DateTime PeriodStartUtc { get; set; } = DateTime.UtcNow.Date;
    public DateTime PeriodEndUtc { get; set; } = DateTime.UtcNow.Date.AddMonths(1);
    [MaxLength(64)]
    public string? PayGatewayPaymentCode { get; set; }
    /// <summary>Crédits de publication consommables (1 crédit = 1 agent publié au-delà du quota inclus).</summary>
    public int PublishCredits { get; set; }
    /// <summary>Runs supplémentaires achetés via packs consommables.</summary>
    public int ConsumableRunsBalance { get; set; }

    public Organization? Organization { get; set; }
    public SubscriptionPlan? SubscriptionPlan { get; set; }
}

public class SubscriptionCheckout : BaseEntity, ITenantEntity
{
    public Guid OrganizationId { get; set; }
    public Guid SubscriptionPlanId { get; set; }
    public CheckoutKind Kind { get; set; } = CheckoutKind.Subscription;
    public int Quantity { get; set; } = 1;
    [MaxLength(64)]
    public required string PaymentCode { get; set; }
    public SubscriptionCheckoutStatus Status { get; set; } = SubscriptionCheckoutStatus.Pending;
    [MaxLength(320)]
    public required string CustomerEmail { get; set; }
    public DateTime? PaidAtUtc { get; set; }

    public Organization? Organization { get; set; }
    public SubscriptionPlan? SubscriptionPlan { get; set; }
}

public class RuntimeHeartbeat : BaseEntity
{
    public required string NodeName { get; set; }
    public required string Status { get; set; }
    public DateTime LastSeenUtc { get; set; } = DateTime.UtcNow;
    public int ActiveTriggerCount { get; set; }
}

public class StudioDomainRequest : BaseEntity, ITenantEntity
{
    public Guid OrganizationId { get; set; }
    [MaxLength(320)]
    public required string RequestedByEmail { get; set; }
    [MaxLength(150)]
    public required string RequestedByName { get; set; }
    [MaxLength(120)]
    public required string DomainName { get; set; }
    [MaxLength(120)]
    public string? Industry { get; set; }
    [MaxLength(2000)]
    public string? UseCase { get; set; }
    [MaxLength(2000)]
    public string? Description { get; set; }
    public DomainRequestStatus Status { get; set; } = DomainRequestStatus.Pending;
}

public class StudioObjectiveRequest : BaseEntity, ITenantEntity
{
    public Guid OrganizationId { get; set; }
    [MaxLength(320)]
    public required string RequestedByEmail { get; set; }
    [MaxLength(150)]
    public required string RequestedByName { get; set; }
    [MaxLength(120)]
    public required string ObjectiveName { get; set; }
    [MaxLength(120)]
    public string? RelatedDomain { get; set; }
    [MaxLength(2000)]
    public string? UseCase { get; set; }
    [MaxLength(2000)]
    public string? Description { get; set; }
    public DomainRequestStatus Status { get; set; } = DomainRequestStatus.Pending;
}

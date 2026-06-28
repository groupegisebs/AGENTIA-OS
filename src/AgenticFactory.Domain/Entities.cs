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
}

public class OrganizationSubscription : BaseEntity, ITenantEntity
{
    public Guid OrganizationId { get; set; }
    public Guid SubscriptionPlanId { get; set; }
    public bool IsActive { get; set; } = true;
    public int UsedRunsThisMonth { get; set; }
    public DateTime PeriodStartUtc { get; set; } = DateTime.UtcNow.Date;
    public DateTime PeriodEndUtc { get; set; } = DateTime.UtcNow.Date.AddMonths(1);

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

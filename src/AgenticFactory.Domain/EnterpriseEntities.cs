using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AgenticFactory.Domain;

/// <summary>Per-node / per-action execution telemetry for a run.</summary>
public sealed class ActionExecutionLog : BaseEntity, ITenantEntity
{
    public Guid OrganizationId { get; set; }
    public Guid RunId { get; set; }
    public Guid AgentId { get; set; }
    public Guid? AgentActionId { get; set; }
    public Guid? ExecutionProviderId { get; set; }

    [MaxLength(120)]
    public string NodeId { get; set; } = string.Empty;

    [MaxLength(120)]
    public string NodeType { get; set; } = string.Empty;

    [MaxLength(200)]
    public string Label { get; set; } = string.Empty;

    [MaxLength(80)]
    public string ProviderType { get; set; } = "graph-runtime";

    [MaxLength(40)]
    public string Status { get; set; } = string.Empty;

    public int DurationMs { get; set; }
    public string? ErrorMessage { get; set; }
    public int RetryCount { get; set; }
    public string? InputJson { get; set; }
    public string? OutputJson { get; set; }
    public DateTime StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }

    public AgentRun? Run { get; set; }
    public Agent? Agent { get; set; }
}

public sealed class OrganizationSecret : BaseEntity, ITenantEntity
{
    public Guid OrganizationId { get; set; }

    [MaxLength(120)]
    public required string Name { get; set; }

    [MaxLength(80)]
    public string? Provider { get; set; }

    /// <summary>Base64-encoded ciphertext (AES protected).</summary>
    public required string CipherText { get; set; }
}

public sealed class AgentMemoryEntry : BaseEntity, ITenantEntity
{
    public Guid OrganizationId { get; set; }
    public Guid AgentId { get; set; }
    public Guid? RunId { get; set; }

    [MaxLength(120)]
    public required string Key { get; set; }

    public required string Value { get; set; }
    public bool IsShortTerm { get; set; } = true;
    public DateTime? ExpiresAtUtc { get; set; }

    public Agent? Agent { get; set; }
}

public sealed class KnowledgeBase : BaseEntity, ITenantEntity
{
    public Guid OrganizationId { get; set; }
    public Guid AgentId { get; set; }

    [MaxLength(160)]
    public required string Name { get; set; }

    public Agent? Agent { get; set; }
    public ICollection<KnowledgeDocument> Documents { get; set; } = new List<KnowledgeDocument>();
}

public sealed class KnowledgeDocument : BaseEntity, ITenantEntity
{
    public Guid OrganizationId { get; set; }
    public Guid KnowledgeBaseId { get; set; }

    [MaxLength(200)]
    public required string Title { get; set; }

    public required string Content { get; set; }

    [MaxLength(40)]
    public string ContentType { get; set; } = "text/plain";

    public KnowledgeBase? KnowledgeBase { get; set; }
}

public sealed class MarketplaceListing : BaseEntity, ITenantEntity
{
    public Guid OrganizationId { get; set; }
    public Guid AgentId { get; set; }

    [MaxLength(160)]
    public required string Title { get; set; }

    public required string Description { get; set; }

    [MaxLength(80)]
    public string Category { get; set; } = "General";

    [MaxLength(40)]
    public string License { get; set; } = "Mensuelle";

    [Column(TypeName = "numeric(18,4)")]
    public decimal PriceUsd { get; set; }

    [MaxLength(40)]
    public string Status { get; set; } = "Draft";

    public int PublishedVersionNumber { get; set; }

    [MaxLength(320)]
    public string? AuthorDisplayName { get; set; }

    [MaxLength(500)]
    public string? DocumentationUrl { get; set; }

    [MaxLength(64)]
    public string? PayGatewayProductCode { get; set; }

    public Agent? Agent { get; set; }
}

public sealed class AuditLogEntry : BaseEntity, ITenantEntity
{
    public Guid OrganizationId { get; set; }

    [MaxLength(80)]
    public required string Action { get; set; }

    [MaxLength(320)]
    public string? ActorEmail { get; set; }

    [MaxLength(120)]
    public string? ResourceType { get; set; }

    public Guid? ResourceId { get; set; }

    public string DetailsJson { get; set; } = "{}";
}

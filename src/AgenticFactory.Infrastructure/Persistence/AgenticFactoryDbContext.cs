using AgenticFactory.Domain;
using AgenticFactory.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AgenticFactory.Infrastructure.Persistence;

public class AgenticFactoryDbContext
    : IdentityDbContext<AppIdentityUser, IdentityRole<Guid>, Guid>
{
    public AgenticFactoryDbContext(DbContextOptions<AgenticFactoryDbContext> options) : base(options)
    {
    }

    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<ApplicationUser> ApplicationUsers => Set<ApplicationUser>();
    public DbSet<Agent> Agents => Set<Agent>();
    public DbSet<AgentBlueprint> AgentBlueprints => Set<AgentBlueprint>();
    public DbSet<AgentVersion> AgentVersions => Set<AgentVersion>();
    public DbSet<AgentDeployment> AgentDeployments => Set<AgentDeployment>();
    public DbSet<AgentRun> AgentRuns => Set<AgentRun>();
    public DbSet<AgentTool> AgentTools => Set<AgentTool>();
    public DbSet<AgentTrigger> AgentTriggers => Set<AgentTrigger>();
    public DbSet<SubscriptionPlan> SubscriptionPlans => Set<SubscriptionPlan>();
    public DbSet<OrganizationSubscription> OrganizationSubscriptions => Set<OrganizationSubscription>();
    public DbSet<SubscriptionCheckout> SubscriptionCheckouts => Set<SubscriptionCheckout>();
    public DbSet<RuntimeHeartbeat> RuntimeHeartbeats => Set<RuntimeHeartbeat>();
    public DbSet<StudioDomainRequest> StudioDomainRequests => Set<StudioDomainRequest>();
    public DbSet<StudioObjectiveRequest> StudioObjectiveRequests => Set<StudioObjectiveRequest>();
    public DbSet<ActionExecutionProvider> ActionExecutionProviders => Set<ActionExecutionProvider>();

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        EnforceTenantAndTimestamps();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        EnforceTenantAndTimestamps();
        return base.SaveChanges();
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Organization>(entity =>
        {
            entity.HasIndex(x => x.Slug).IsUnique();
            entity.Property(x => x.Name).HasMaxLength(150);
            entity.Property(x => x.Slug).HasMaxLength(160);
        });

        builder.Entity<ApplicationUser>(entity =>
        {
            entity.HasIndex(x => new { x.OrganizationId, x.Email }).IsUnique();
            entity.Property(x => x.Email).HasMaxLength(320);
            entity.HasOne(x => x.Organization)
                .WithMany(x => x.Users)
                .HasForeignKey(x => x.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Agent>(entity =>
        {
            entity.HasIndex(x => x.EndpointSlug).IsUnique();
            entity.HasIndex(x => new { x.OrganizationId, x.Name });
            entity.HasOne(x => x.Organization)
                .WithMany(x => x.Agents)
                .HasForeignKey(x => x.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<AgentBlueprint>(entity =>
        {
            entity.HasIndex(x => new { x.OrganizationId, x.AgentId, x.Status });
            entity.HasOne(x => x.Agent).WithMany().HasForeignKey(x => x.AgentId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<AgentVersion>(entity =>
        {
            entity.HasIndex(x => new { x.OrganizationId, x.AgentId, x.VersionNumber }).IsUnique();
            entity.HasOne(x => x.Agent).WithMany(x => x.Versions).HasForeignKey(x => x.AgentId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<AgentDeployment>(entity =>
        {
            entity.HasIndex(x => new { x.OrganizationId, x.AgentId, x.Status });
            entity.HasOne(x => x.Agent).WithMany().HasForeignKey(x => x.AgentId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.AgentVersion).WithMany(x => x.Deployments).HasForeignKey(x => x.AgentVersionId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<AgentRun>(entity =>
        {
            entity.HasIndex(x => new { x.OrganizationId, x.AgentId, x.CreatedAtUtc });
            entity.HasOne(x => x.Agent).WithMany().HasForeignKey(x => x.AgentId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.AgentVersion).WithMany().HasForeignKey(x => x.AgentVersionId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<AgentTool>(entity => entity.HasIndex(x => new { x.OrganizationId, x.AgentId, x.Name }));
        builder.Entity<AgentTrigger>(entity =>
        {
            entity.HasIndex(x => new { x.OrganizationId, x.AgentId, x.IsEnabled });
            entity.HasOne(x => x.Agent).WithMany(x => x.Triggers).HasForeignKey(x => x.AgentId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<SubscriptionPlan>(entity => entity.HasIndex(x => x.Name).IsUnique());
        builder.Entity<OrganizationSubscription>(entity =>
        {
            entity.HasIndex(x => new { x.OrganizationId, x.IsActive });
            entity.HasOne(x => x.Organization).WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.SubscriptionPlan).WithMany().HasForeignKey(x => x.SubscriptionPlanId).OnDelete(DeleteBehavior.Restrict);
            entity.Property(x => x.PayGatewayPaymentCode).HasMaxLength(64);
        });

        builder.Entity<SubscriptionCheckout>(entity =>
        {
            entity.HasIndex(x => x.PaymentCode).IsUnique();
            entity.HasIndex(x => new { x.OrganizationId, x.Status, x.CreatedAtUtc });
            entity.Property(x => x.PaymentCode).HasMaxLength(64);
            entity.Property(x => x.CustomerEmail).HasMaxLength(320);
            entity.HasOne(x => x.Organization).WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.SubscriptionPlan).WithMany().HasForeignKey(x => x.SubscriptionPlanId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<RuntimeHeartbeat>(entity => entity.HasIndex(x => x.NodeName).IsUnique());
        builder.Entity<AppIdentityUser>(entity => entity.HasIndex(x => new { x.OrganizationId, x.Email }).IsUnique());

        builder.Entity<StudioDomainRequest>(entity =>
        {
            entity.HasIndex(x => new { x.OrganizationId, x.Status, x.CreatedAtUtc });
            entity.Property(x => x.DomainName).HasMaxLength(120);
            entity.Property(x => x.RequestedByEmail).HasMaxLength(320);
            entity.Property(x => x.RequestedByName).HasMaxLength(150);
        });

        builder.Entity<StudioObjectiveRequest>(entity =>
        {
            entity.HasIndex(x => new { x.OrganizationId, x.Status, x.CreatedAtUtc });
            entity.Property(x => x.ObjectiveName).HasMaxLength(120);
            entity.Property(x => x.RelatedDomain).HasMaxLength(120);
            entity.Property(x => x.RequestedByEmail).HasMaxLength(320);
            entity.Property(x => x.RequestedByName).HasMaxLength(150);
        });

        builder.Entity<ActionExecutionProvider>(entity =>
        {
            entity.HasIndex(x => x.ProviderType).IsUnique();
            entity.HasIndex(x => x.IsEnabled);
            entity.Property(x => x.Name).HasMaxLength(120);
            entity.Property(x => x.Description).HasMaxLength(500);
            entity.Property(x => x.Category).HasMaxLength(80);
            entity.Property(x => x.Version).HasMaxLength(20);
            entity.Property(x => x.Author).HasMaxLength(120);
        });
    }

    private void EnforceTenantAndTimestamps()
    {
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAtUtc = DateTime.UtcNow;
            }

            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAtUtc = DateTime.UtcNow;
                entry.Entity.UpdatedAtUtc = DateTime.UtcNow;
            }
        }

        foreach (var entry in ChangeTracker.Entries<ITenantEntity>())
        {
            if (entry.Entity.OrganizationId == Guid.Empty)
            {
                throw new InvalidOperationException("OrganizationId is required for tenant entities.");
            }
        }
    }
}

using AgentiaOs.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgentiaOs.Infrastructure.Persistence;

public sealed class AgentiaDbContext(DbContextOptions<AgentiaDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<ConversationMessage> ConversationMessages => Set<ConversationMessage>();
    public DbSet<DeploymentJob> DeploymentJobs => Set<DeploymentJob>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.Email).IsUnique();
            entity.Property(x => x.Email).HasMaxLength(200);
            entity.Property(x => x.DisplayName).HasMaxLength(120);
            entity.Property(x => x.PasswordHash).HasMaxLength(500);
        });

        modelBuilder.Entity<Conversation>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Title).HasMaxLength(200);
            entity.HasMany(x => x.Messages)
                .WithOne(x => x.Conversation)
                .HasForeignKey(x => x.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ConversationMessage>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Role).HasMaxLength(20);
            entity.Property(x => x.Content).HasMaxLength(4_000);
        });

        modelBuilder.Entity<DeploymentJob>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.TargetEnvironment).HasMaxLength(50);
            entity.Property(x => x.Status).HasMaxLength(30);
        });
    }
}

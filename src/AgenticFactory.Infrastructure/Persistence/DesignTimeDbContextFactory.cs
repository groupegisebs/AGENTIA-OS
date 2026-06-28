using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AgenticFactory.Infrastructure.Persistence;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AgenticFactoryDbContext>
{
    public AgenticFactoryDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AgenticFactoryDbContext>();
        var connectionString = Environment.GetEnvironmentVariable("DATABASE_CONNECTION_STRING")
            ?? "Host=localhost;Port=5432;Database=agentic_factory;Username=postgres;Password=postgres";

        optionsBuilder.UseNpgsql(connectionString);
        return new AgenticFactoryDbContext(optionsBuilder.Options);
    }
}

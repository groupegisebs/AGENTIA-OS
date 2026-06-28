using AgentiaOs.Application.Abstractions;
using AgentiaOs.Infrastructure.Auth;
using AgentiaOs.Infrastructure.Configuration;
using AgentiaOs.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AgentiaOs.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));

        var connectionString = configuration.GetConnectionString("Default")
            ?? "Data Source=agentiaos.db";

        services.AddDbContext<AgentiaDbContext>(options => options.UseSqlite(connectionString));
        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();

        return services;
    }
}

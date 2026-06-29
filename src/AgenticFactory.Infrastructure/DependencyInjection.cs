using System.Text;
using AgenticFactory.Application;
using AgenticFactory.Domain;
using AgenticFactory.Infrastructure.Billing;
using AgenticFactory.Infrastructure.Identity;
using AgenticFactory.Infrastructure.Persistence;
using AgenticFactory.Infrastructure.Services;
using AgenticFactory.Infrastructure.Services.ExecutionProviders;
using AgenticFactory.Infrastructure.Services.ExecutionProviders.Generators;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Trace;
using Serilog;

namespace AgenticFactory.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddAgenticInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var provider = (configuration["Database:Provider"] ?? Environment.GetEnvironmentVariable("DATABASE_PROVIDER") ?? "postgres").ToLowerInvariant();
        var connectionString = configuration.GetConnectionString("Default")
            ?? Environment.GetEnvironmentVariable("DATABASE_CONNECTION_STRING")
            ?? "Host=localhost;Port=5432;Database=agentic_factory;Username=postgres;Password=postgres";

        services.AddDbContext<AgenticFactoryDbContext>(options =>
        {
            if (string.Equals(provider, "sqlserver", StringComparison.OrdinalIgnoreCase))
            {
                options.UseSqlServer(connectionString);
                return;
            }

            if (string.Equals(provider, "inmemory", StringComparison.OrdinalIgnoreCase))
            {
                options.UseInMemoryDatabase("agentic-factory-dev");
                return;
            }

            options.UseNpgsql(connectionString);
        });

        services.AddHttpContextAccessor();
        services.AddHttpClient();
        services.Configure<GisebsApiPayGatewayOptions>(configuration.GetSection(GisebsApiPayGatewayOptions.SectionName));
        services.Configure<BillingOptions>(configuration.GetSection(BillingOptions.SectionName));
        services.AddHttpClient(nameof(GisebsPayGatewayClient))
            .ConfigurePrimaryHttpMessageHandler(GisebsPayGatewayHttp.CreateHandler);
        services.AddScoped<IGisebsPayGatewayClient, GisebsPayGatewayClient>();
        services.AddScoped<GisebsPayGatewayCatalogSync>();
        services.AddScoped<ISubscriptionBillingService, SubscriptionBillingService>();
        services.AddScoped<IPublishEligibilityService, PublishEligibilityService>();
        services.AddIdentity<AppIdentityUser, IdentityRole<Guid>>(options =>
            {
                options.Password.RequireDigit = true;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = true;
                options.User.RequireUniqueEmail = true;
            })
            .AddEntityFrameworkStores<AgenticFactoryDbContext>()
            .AddDefaultTokenProviders();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                var key = configuration["Auth:JwtKey"] ?? "change-this-development-key-min-32-characters";
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateIssuerSigningKey = true,
                    ValidateLifetime = true,
                    ValidIssuer = configuration["Auth:Issuer"] ?? "AgenticFactory",
                    ValidAudience = configuration["Auth:Audience"] ?? "AgenticFactoryClients",
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key))
                };
            });

        services.AddAuthorization(options =>
        {
            options.AddPolicy("CanCreateAgent", policy => policy.RequireRole("Admin", "Creator"));
            options.AddPolicy("CanViewDashboard", policy => policy.RequireRole("Admin", "Creator", "Viewer"));
        });

        services.AddScoped<ICurrentTenantService, CurrentTenantService>();
        services.AddScoped<IBlueprintGenerator, MockBlueprintGenerator>();
        services.AddScoped<IAgentCreationService, AgentCreationService>();
        services.AddScoped<IAgentDeploymentService, AgentDeploymentService>();
        services.AddScoped<IAgentInvocationService, AgentInvocationService>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IAgentExecutor, AgentExecutor>();
        services.AddScoped<IAgentToolExecutor, AgentToolExecutor>();
        services.AddScoped<IAgentMemoryService, AgentMemoryService>();
        services.AddScoped<IAgentModelProvider, AgentModelProvider>();
        services.AddScoped<IAgentRuntime, RuntimeEngine>();
        services.AddScoped<IdentitySeedService>();

        services.AddSingleton<WindowsRuntimeProvider>();
        services.AddSingleton<IEnumerable<IExecutionProvider>>(sp =>
        {
            var runtime = sp.GetRequiredService<WindowsRuntimeProvider>();
            ExecutionProviderType[] stubTypes =
            [
                ExecutionProviderType.PowerAutomate,
                ExecutionProviderType.LogicApps,
                ExecutionProviderType.N8n,
                ExecutionProviderType.Webhook,
                ExecutionProviderType.RestApi,
                ExecutionProviderType.PowerShell,
                ExecutionProviderType.Python,
                ExecutionProviderType.DockerJob,
                ExecutionProviderType.AzureFunction,
                ExecutionProviderType.WindowsScript
            ];
            IExecutionProvider[] providers = [runtime, .. stubTypes.Select(t => new StubExecutionProvider(t))];
            return providers;
        });
        services.AddSingleton<IExecutionProviderRegistry, ExecutionProviderRegistry>();
        services.AddScoped<IExecutionProviderRecommendationService, ExecutionProviderRecommendationService>();
        services.AddScoped<IExecutionProviderCatalogService, ExecutionProviderCatalogService>();
        services.AddSingleton<IPowerAutomateGenerator, PowerAutomateGenerator>();
        services.AddSingleton<ILogicAppGenerator, LogicAppGenerator>();
        services.AddSingleton<IN8nWorkflowGenerator, N8nWorkflowGenerator>();

        services.AddOpenTelemetry()
            .WithTracing(builder => builder
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddConsoleExporter());

        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .WriteTo.File("logs/agentic-factory-.log", rollingInterval: RollingInterval.Day)
            .Enrich.FromLogContext()
            .CreateLogger();

        services.AddSerilog();
        return services;
    }
}

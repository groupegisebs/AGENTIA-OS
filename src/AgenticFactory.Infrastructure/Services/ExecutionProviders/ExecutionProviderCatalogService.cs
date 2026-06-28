using System.Text.Json;
using AgenticFactory.Application;
using AgenticFactory.Domain;
using AgenticFactory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgenticFactory.Infrastructure.Services.ExecutionProviders;

public static class ExecutionProviderSeed
{
    public static readonly Guid InternalRuntimeId = Guid.Parse("a1000001-0001-4001-8001-000000000001");
    public static readonly Guid PowerAutomateId = Guid.Parse("a1000001-0001-4001-8001-000000000002");
    public static readonly Guid LogicAppsId = Guid.Parse("a1000001-0001-4001-8001-000000000003");
    public static readonly Guid N8nId = Guid.Parse("a1000001-0001-4001-8001-000000000004");
    public static readonly Guid WebhookId = Guid.Parse("a1000001-0001-4001-8001-000000000005");
    public static readonly Guid RestApiId = Guid.Parse("a1000001-0001-4001-8001-000000000006");
    public static readonly Guid PowerShellId = Guid.Parse("a1000001-0001-4001-8001-000000000007");
    public static readonly Guid PythonId = Guid.Parse("a1000001-0001-4001-8001-000000000008");
    public static readonly Guid DockerJobId = Guid.Parse("a1000001-0001-4001-8001-000000000009");
    public static readonly Guid AzureFunctionId = Guid.Parse("a1000001-0001-4001-8001-00000000000a");

    private static readonly Dictionary<Guid, ExecutionProviderType> IdToType = new()
    {
        [InternalRuntimeId] = ExecutionProviderType.InternalRuntime,
        [PowerAutomateId] = ExecutionProviderType.PowerAutomate,
        [LogicAppsId] = ExecutionProviderType.LogicApps,
        [N8nId] = ExecutionProviderType.N8n,
        [WebhookId] = ExecutionProviderType.Webhook,
        [RestApiId] = ExecutionProviderType.RestApi,
        [PowerShellId] = ExecutionProviderType.PowerShell,
        [PythonId] = ExecutionProviderType.Python,
        [DockerJobId] = ExecutionProviderType.DockerJob,
        [AzureFunctionId] = ExecutionProviderType.AzureFunction
    };

    public static bool TryGetType(Guid id, out ExecutionProviderType type) => IdToType.TryGetValue(id, out type);

    public static IReadOnlyList<ActionExecutionProvider> CreateSystemProviders() =>
    [
        Create(InternalRuntimeId, "Windows Runtime", "Exécution locale via le Windows Service orchestrateur Agentia.", "Runtime", ExecutionProviderType.InternalRuntime,
            supportsMonitoring: true, supportsRetry: true, supportsRollback: true, supportsScheduling: true),
        Create(PowerAutomateId, "Power Automate", "Flows cloud Microsoft pour Teams, Outlook, SharePoint et connecteurs M365.", "Cloud", ExecutionProviderType.PowerAutomate,
            supportsParameters: true, supportsMonitoring: true, supportsRetry: true, supportsScheduling: true),
        Create(LogicAppsId, "Logic Apps", "Workflows Azure Logic Apps pour intégrations enterprise et API.", "Cloud", ExecutionProviderType.LogicApps,
            supportsParameters: true, supportsMonitoring: true, supportsRetry: true, supportsScheduling: true),
        Create(N8nId, "n8n", "Automatisation open-source self-hosted ou cloud.", "Automation", ExecutionProviderType.N8n,
            supportsParameters: true, supportsMonitoring: true, supportsRetry: true),
        Create(WebhookId, "Webhook", "Déclenchement HTTP entrant/sortant vers endpoints externes.", "Integration", ExecutionProviderType.Webhook,
            supportsParameters: true, supportsMonitoring: true, supportsRetry: true),
        Create(RestApiId, "REST API", "Appels REST configurables avec authentification et mapping JSON.", "Integration", ExecutionProviderType.RestApi,
            supportsParameters: true, supportsMonitoring: true, supportsRetry: true),
        Create(PowerShellId, "PowerShell", "Scripts PowerShell locaux ou distants (WinRM).", "Script", ExecutionProviderType.PowerShell,
            supportsParameters: true, supportsRetry: true),
        Create(PythonId, "Python", "Scripts Python containerisés ou locaux.", "Script", ExecutionProviderType.Python,
            supportsParameters: true, supportsRetry: true),
        Create(DockerJobId, "Docker", "Jobs containerisés exécutés via Docker.", "Container", ExecutionProviderType.DockerJob,
            supportsParameters: true, supportsMonitoring: true, supportsRetry: true),
        Create(AzureFunctionId, "Azure Function", "Functions serverless Azure pour scalabilité cloud.", "Cloud", ExecutionProviderType.AzureFunction,
            supportsParameters: true, supportsMonitoring: true, supportsRetry: true, supportsScheduling: true)
    ];

    public static async Task SeedAsync(AgenticFactoryDbContext db, CancellationToken cancellationToken)
    {
        if (await db.ActionExecutionProviders.AnyAsync(cancellationToken))
            return;

        foreach (var provider in CreateSystemProviders())
            db.ActionExecutionProviders.Add(provider);

        await db.SaveChangesAsync(cancellationToken);
    }

    private static ActionExecutionProvider Create(
        Guid id, string name, string description, string category, ExecutionProviderType type,
        bool supportsParameters = false, bool supportsMonitoring = false,
        bool supportsRetry = false, bool supportsRollback = false, bool supportsScheduling = false)
    {
        var now = DateTime.UtcNow;
        return new ActionExecutionProvider
        {
            Id = id,
            Name = name,
            Description = description,
            Category = category,
            ProviderType = type,
            IsSystem = true,
            SupportsParameters = supportsParameters,
            SupportsMonitoring = supportsMonitoring,
            SupportsRetry = supportsRetry,
            SupportsRollback = supportsRollback,
            SupportsScheduling = supportsScheduling,
            Version = "1.0.0",
            Author = "Agentia",
            IsEnabled = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
    }
}

public sealed class ExecutionProviderCatalogService(
    AgenticFactoryDbContext dbContext,
    IPowerAutomateGenerator powerAutomateGenerator,
    ILogicAppGenerator logicAppGenerator,
    IN8nWorkflowGenerator n8nWorkflowGenerator) : IExecutionProviderCatalogService
{
    public async Task<IReadOnlyList<ActionExecutionProvider>> ListAsync(bool enabledOnly, CancellationToken cancellationToken)
    {
        var query = dbContext.ActionExecutionProviders.AsNoTracking();
        if (enabledOnly)
            query = query.Where(x => x.IsEnabled);
        return await query.OrderBy(x => x.Name).ToListAsync(cancellationToken);
    }

    public async Task<ActionExecutionProvider?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        => await dbContext.ActionExecutionProviders.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<GeneratedFlowResult?> GeneratePreviewAsync(
        Guid providerId, GenerateFlowRequest request, CancellationToken cancellationToken)
    {
        var provider = await GetByIdAsync(providerId, cancellationToken);
        if (provider is null)
            return null;

        return provider.ProviderType switch
        {
            ExecutionProviderType.PowerAutomate => powerAutomateGenerator.GenerateFlow(request),
            ExecutionProviderType.LogicApps => logicAppGenerator.GenerateWorkflow(request),
            ExecutionProviderType.N8n => n8nWorkflowGenerator.GenerateWorkflow(request),
            _ => new GeneratedFlowResult(
                provider.ProviderType.ToString(),
                "json",
                JsonSerializer.Serialize(new { message = "Preview not available for this provider type." }))
        };
    }
}

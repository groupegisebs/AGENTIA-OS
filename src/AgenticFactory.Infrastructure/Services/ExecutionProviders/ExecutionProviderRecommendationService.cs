using AgenticFactory.Application;
using AgenticFactory.Domain;
using AgenticFactory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgenticFactory.Infrastructure.Services.ExecutionProviders;

public sealed class ExecutionProviderRecommendationService(AgenticFactoryDbContext dbContext)
    : IExecutionProviderRecommendationService
{
    public async Task<ProviderRecommendation?> RecommendProviderAsync(
        RecommendProviderRequest request,
        CancellationToken cancellationToken)
    {
        var providers = await dbContext.ActionExecutionProviders
            .AsNoTracking()
            .Where(x => x.IsEnabled)
            .ToListAsync(cancellationToken);

        if (providers.Count == 0)
            return null;

        var actuator = request.ActuatorType?.ToLowerInvariant() ?? string.Empty;
        var sensors = request.Sensors.Select(s => s.ToLowerInvariant()).ToHashSet();
        var tools = request.Tools.Select(t => t.ToLowerInvariant()).ToHashSet();

        ActionExecutionProvider? chosen = null;
        string reason = string.Empty;
        double confidence = 0.6;

        if (actuator is "notify-teams" or "notify-slack" or "create-ticket" or "create-event" or "trigger-workflow")
        {
            chosen = providers.FirstOrDefault(p => p.ProviderType == ExecutionProviderType.PowerAutomate);
            reason = "Intégration Microsoft 365 / Teams / ticketing — Power Automate est le connecteur natif.";
            confidence = 0.88;
        }
        else if (actuator is "call-api" or "upload" or "download")
        {
            if (tools.Any(t => t.Contains("webhook") || t.Contains("http")))
            {
                chosen = providers.FirstOrDefault(p => p.ProviderType == ExecutionProviderType.Webhook);
                reason = "Appel HTTP sortant — Webhook ou REST API selon votre stack.";
                confidence = 0.82;
            }
            else
            {
                chosen = providers.FirstOrDefault(p => p.ProviderType == ExecutionProviderType.RestApi);
                reason = "Action API REST — provider REST API avec retry et monitoring.";
                confidence = 0.8;
            }
        }
        else if (actuator is "modify-sql" or "move-file")
        {
            chosen = providers.FirstOrDefault(p => p.ProviderType == ExecutionProviderType.PowerShell)
                ?? providers.FirstOrDefault(p => p.ProviderType == ExecutionProviderType.InternalRuntime);
            reason = "Manipulation fichiers / SQL locale — PowerShell ou Runtime Windows Service.";
            confidence = 0.75;
        }
        else if (actuator is "launch-agent" or "create-task")
        {
            chosen = providers.FirstOrDefault(p => p.ProviderType == ExecutionProviderType.LogicApps)
                ?? providers.FirstOrDefault(p => p.ProviderType == ExecutionProviderType.N8n);
            reason = "Orchestration multi-étapes — Logic Apps ou n8n pour chaîner des workflows.";
            confidence = 0.78;
        }
        else if (sensors.Any(s => s.Contains("azure") || s.Contains("cloud")))
        {
            chosen = providers.FirstOrDefault(p => p.ProviderType == ExecutionProviderType.AzureFunction);
            reason = "Contexte cloud Azure — Azure Function serverless recommandée.";
            confidence = 0.85;
        }
        else if (actuator is "send-email" or "create-document" or "create-report")
        {
            chosen = providers.FirstOrDefault(p => p.ProviderType == ExecutionProviderType.InternalRuntime);
            reason = "Action documentaire standard — exécution via Runtime Windows Service orchestrateur.";
            confidence = 0.7;
        }

        chosen ??= providers.First(p => p.ProviderType == ExecutionProviderType.InternalRuntime);
        reason = string.IsNullOrWhiteSpace(reason)
            ? "Runtime Windows Service — orchestrateur principal Agentia, exécution locale fiable."
            : reason;

        return new ProviderRecommendation(
            chosen.Id,
            chosen.Name,
            chosen.ProviderType,
            reason,
            confidence);
    }
}

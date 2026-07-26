using System.Text;
using System.Text.Json;
using AgenticFactory.Application;
using AgenticFactory.Domain;
using AgenticFactory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AgenticFactory.Infrastructure.Services;

public sealed class AgentMemoryService(AgenticFactoryDbContext db) : IAgentMemoryService
{
    public Task RememberAsync(Guid runId, string data, CancellationToken cancellationToken)
        => RememberEntryAsync(Guid.Empty, Guid.Empty, runId, "run-output", data, true, cancellationToken);

    public async Task RememberEntryAsync(
        Guid organizationId,
        Guid agentId,
        Guid runId,
        string key,
        string value,
        bool isShortTerm,
        CancellationToken cancellationToken)
    {
        if (organizationId == Guid.Empty || agentId == Guid.Empty)
        {
            var run = await db.AgentRuns.AsNoTracking().FirstOrDefaultAsync(x => x.Id == runId, cancellationToken);
            if (run is null) return;
            organizationId = run.OrganizationId;
            agentId = run.AgentId;
        }

        db.AgentMemoryEntries.Add(new AgentMemoryEntry
        {
            OrganizationId = organizationId,
            AgentId = agentId,
            RunId = runId == Guid.Empty ? null : runId,
            Key = key,
            Value = value,
            IsShortTerm = isShortTerm,
            ExpiresAtUtc = isShortTerm ? DateTime.UtcNow.AddDays(7) : null
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AgentMemorySnapshot>> GetRecentAsync(
        Guid organizationId, Guid agentId, int take, CancellationToken cancellationToken)
    {
        return await db.AgentMemoryEntries.AsNoTracking()
            .Where(x => x.OrganizationId == organizationId && x.AgentId == agentId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(Math.Clamp(take, 1, 200))
            .Select(x => new AgentMemorySnapshot(x.Id, x.Key, x.Value, x.IsShortTerm, x.CreatedAtUtc))
            .ToListAsync(cancellationToken);
    }
}

public sealed class KnowledgeService(AgenticFactoryDbContext db) : IKnowledgeService
{
    public async Task<Guid> CreateBaseAsync(Guid organizationId, Guid agentId, string name, CancellationToken cancellationToken)
    {
        var kb = new KnowledgeBase
        {
            OrganizationId = organizationId,
            AgentId = agentId,
            Name = name
        };
        db.KnowledgeBases.Add(kb);
        await db.SaveChangesAsync(cancellationToken);
        return kb.Id;
    }

    public async Task<Guid> IngestDocumentAsync(
        Guid organizationId, Guid knowledgeBaseId, string title, string content, CancellationToken cancellationToken)
    {
        var kb = await db.KnowledgeBases.FirstAsync(
            x => x.Id == knowledgeBaseId && x.OrganizationId == organizationId, cancellationToken);
        var doc = new KnowledgeDocument
        {
            OrganizationId = organizationId,
            KnowledgeBaseId = kb.Id,
            Title = title,
            Content = content
        };
        db.KnowledgeDocuments.Add(doc);
        await db.SaveChangesAsync(cancellationToken);
        return doc.Id;
    }

    public async Task<IReadOnlyList<KnowledgeHit>> SearchAsync(
        Guid organizationId, Guid agentId, string query, int topK, CancellationToken cancellationToken)
    {
        var docs = await db.KnowledgeDocuments.AsNoTracking()
            .Where(d => d.OrganizationId == organizationId
                        && db.KnowledgeBases.Any(kb => kb.Id == d.KnowledgeBaseId && kb.AgentId == agentId))
            .ToListAsync(cancellationToken);

        var terms = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(t => t.ToLowerInvariant())
            .Where(t => t.Length > 2)
            .Distinct()
            .ToArray();

        return docs
            .Select(d =>
            {
                var text = (d.Title + " " + d.Content).ToLowerInvariant();
                var score = terms.Length == 0
                    ? 0.1
                    : terms.Count(t => text.Contains(t)) / (double)terms.Length;
                var snippet = d.Content.Length <= 240 ? d.Content : d.Content[..240] + "…";
                return new KnowledgeHit(d.Id, d.Title, snippet, score);
            })
            .Where(h => h.Score > 0)
            .OrderByDescending(h => h.Score)
            .Take(Math.Clamp(topK, 1, 20))
            .ToList();
    }
}

public sealed class MarketplaceService(AgenticFactoryDbContext db) : IMarketplaceService
{
    public async Task<MarketplaceListingDto> PublishAsync(
        Guid organizationId, Guid agentId, MarketplacePublishRequest request, CancellationToken cancellationToken)
    {
        var agent = await db.Agents.FirstAsync(x => x.Id == agentId && x.OrganizationId == organizationId, cancellationToken);
        var version = await db.AgentVersions
            .Where(x => x.AgentId == agentId && x.IsActive)
            .OrderByDescending(x => x.VersionNumber)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("Aucune version active à publier.");

        var listing = await db.MarketplaceListings
            .FirstOrDefaultAsync(x => x.OrganizationId == organizationId && x.AgentId == agentId, cancellationToken);

        if (listing is null)
        {
            listing = new MarketplaceListing
            {
                OrganizationId = organizationId,
                AgentId = agentId,
                Title = agent.Name,
                Description = request.Description,
                Category = request.Category,
                License = request.License,
                PriceUsd = request.PriceUsd,
                Status = "Published",
                PublishedVersionNumber = version.VersionNumber,
                DocumentationUrl = request.DocumentationUrl,
                PayGatewayProductCode = agent.PayGatewayProductCode,
                AuthorDisplayName = "Organisation"
            };
            db.MarketplaceListings.Add(listing);
        }
        else
        {
            listing.Description = request.Description;
            listing.Category = request.Category;
            listing.License = request.License;
            listing.PriceUsd = request.PriceUsd;
            listing.DocumentationUrl = request.DocumentationUrl;
            listing.Status = "Published";
            listing.PublishedVersionNumber = version.VersionNumber;
            listing.UpdatedAtUtc = DateTime.UtcNow;
            listing.PayGatewayProductCode = agent.PayGatewayProductCode;
        }

        agent.Status = AgentStatus.Active;
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(listing, agent.Name);
    }

    public async Task<IReadOnlyList<MarketplaceListingDto>> ListPublishedAsync(CancellationToken cancellationToken)
    {
        var rows = await db.MarketplaceListings.AsNoTracking()
            .Where(x => x.Status == "Published")
            .OrderByDescending(x => x.UpdatedAtUtc)
            .Join(db.Agents.AsNoTracking(), l => l.AgentId, a => a.Id, (l, a) => new { l, a.Name })
            .ToListAsync(cancellationToken);
        return rows.Select(r => ToDto(r.l, r.Name)).ToList();
    }

    public async Task<MarketplaceListingDto?> GetAsync(Guid listingId, CancellationToken cancellationToken)
    {
        var listing = await db.MarketplaceListings.AsNoTracking().FirstOrDefaultAsync(x => x.Id == listingId, cancellationToken);
        if (listing is null) return null;
        var name = await db.Agents.AsNoTracking().Where(a => a.Id == listing.AgentId).Select(a => a.Name).FirstAsync(cancellationToken);
        return ToDto(listing, name);
    }

    public async Task RollbackVersionAsync(Guid organizationId, Guid agentId, int versionNumber, CancellationToken cancellationToken)
    {
        var agent = await db.Agents.FirstAsync(x => x.Id == agentId && x.OrganizationId == organizationId, cancellationToken);
        var versions = await db.AgentVersions
            .Where(x => x.AgentId == agentId && x.OrganizationId == organizationId)
            .ToListAsync(cancellationToken);
        var target = versions.FirstOrDefault(v => v.VersionNumber == versionNumber)
            ?? throw new InvalidOperationException("Version introuvable.");

        foreach (var v in versions) v.IsActive = false;
        target.IsActive = true;
        agent.ActiveVersionId = target.Id;
        agent.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    private static MarketplaceListingDto ToDto(MarketplaceListing l, string agentName) => new(
        l.Id, l.AgentId, agentName, l.AuthorDisplayName ?? "Organisation",
        l.PriceUsd, l.License, l.Description, l.Category, l.Status,
        l.PublishedVersionNumber, l.UpdatedAtUtc, l.PayGatewayProductCode);
}

public sealed class AgentOptimizationService(
    AgenticFactoryDbContext db,
    ILogger<AgentOptimizationService> logger) : IAgentOptimizationService
{
    public async Task<IReadOnlyList<AgentOptimizationSuggestion>> AnalyzeAsync(
        Guid organizationId, Guid agentId, CancellationToken cancellationToken)
    {
        var suggestions = new List<AgentOptimizationSuggestion>();
        var logs = await db.ActionExecutionLogs.AsNoTracking()
            .Where(x => x.OrganizationId == organizationId && x.AgentId == agentId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(200)
            .ToListAsync(cancellationToken);

        var failRate = logs.Count == 0 ? 0 : logs.Count(l => l.Status == "Failed") / (double)logs.Count;
        if (failRate > 0.05)
        {
            suggestions.Add(new AgentOptimizationSuggestion(
                "add-validation",
                "Ajouter une validation",
                $"Taux d'échec observé {(failRate * 100):0.0}%. Ajoutez un nœud Validation avant les actions.",
                "Fiabilité",
                15));
        }

        var avgMs = logs.Count == 0 ? 0 : logs.Average(l => l.DurationMs);
        if (avgMs > 2500)
        {
            suggestions.Add(new AgentOptimizationSuggestion(
                "add-cache",
                "Ajouter un cache",
                $"Temps moyen {avgMs:0} ms. Un cache réduira les appels répétés.",
                "Performance",
                40));
        }

        var hasIa = logs.Any(l => l.NodeType.Contains("skill", StringComparison.OrdinalIgnoreCase)
                                  || l.NodeType.Contains("extraction", StringComparison.OrdinalIgnoreCase)
                                  || l.NodeType.Contains("ocr", StringComparison.OrdinalIgnoreCase));
        if (hasIa)
        {
            suggestions.Add(new AgentOptimizationSuggestion(
                "cheaper-model",
                "Réduire le coût GPT",
                "Utiliser gpt-4o-mini pour les tâches non critiques.",
                "Coût",
                60));
        }

        if (!logs.Any(l => l.NodeType.Contains("util-logs", StringComparison.OrdinalIgnoreCase)))
        {
            suggestions.Add(new AgentOptimizationSuggestion(
                "add-logs",
                "Ajouter un journal",
                "Aucune capacité Journal détectée dans les exécutions récentes.",
                "Observabilité",
                5));
        }

        if (suggestions.Count == 0)
        {
            suggestions.Add(new AgentOptimizationSuggestion(
                "healthy",
                "Agent en bonne santé",
                "Aucune optimisation urgente détectée sur les 200 derniers logs.",
                "OK",
                0));
        }

        logger.LogInformation("Optimization analysis for agent {AgentId}: {Count} suggestions", agentId, suggestions.Count);
        return suggestions;
    }
}

public sealed class ObservatoryService(AgenticFactoryDbContext db) : IObservatoryService
{
    public async Task<ObservatorySnapshot> GetSnapshotAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        var since = DateTime.UtcNow.AddHours(-24);
        var agents = await db.Agents.AsNoTracking()
            .Where(a => a.OrganizationId == organizationId)
            .ToListAsync(cancellationToken);

        var runs = await db.AgentRuns.AsNoTracking()
            .Where(r => r.OrganizationId == organizationId && r.CreatedAtUtc >= since)
            .ToListAsync(cancellationToken);

        var rows = agents.Select(a =>
        {
            var agentRuns = runs.Where(r => r.AgentId == a.Id).ToList();
            var ok = agentRuns.Count(r => r.Status == RunStatus.Completed);
            var success = agentRuns.Count == 0 ? 100 : (ok * 100.0 / agentRuns.Count);
            var status = a.Status == AgentStatus.Active
                ? (success >= 95 ? "green" : success >= 80 ? "yellow" : "red")
                : "gray";
            return new ObservatoryAgentRow(
                a.Id,
                a.Name,
                status,
                agentRuns.Count,
                Math.Round(success, 1),
                agentRuns.Sum(r => r.EstimatedCostUsd));
        }).ToList();

        var completed = runs.Where(r => r.CompletedAtUtc.HasValue && r.StartedAtUtc.HasValue).ToList();
        var avgSec = completed.Count == 0
            ? 0
            : completed.Average(r => (r.CompletedAtUtc!.Value - r.StartedAtUtc!.Value).TotalSeconds);

        var health = rows.Count == 0 ? 100 : rows.Average(r => r.SuccessRate);

        return new ObservatorySnapshot(
            agents.Count(a => a.Status == AgentStatus.Active),
            runs.Count,
            runs.Sum(r => r.EstimatedCostUsd),
            Math.Round(avgSec, 2),
            Math.Round(health, 2),
            rows);
    }
}

/// <summary>LLM blueprint generator with mock fallback.</summary>
public sealed class LlmBlueprintGenerator(
    IConfiguration configuration,
    IAgentModelProvider modelProvider,
    MockBlueprintGenerator mockFallback,
    ILogger<LlmBlueprintGenerator> logger) : IBlueprintGenerator
{
    public async Task<BlueprintResponse> GenerateAsync(Guid organizationId, string message, CancellationToken cancellationToken)
    {
        var mode = (configuration["AI:Mode"] ?? "mock").ToLowerInvariant();
        if (mode is "mock")
            return await mockFallback.GenerateAsync(organizationId, message, cancellationToken);

        try
        {
            var system = """
                Tu génères un CapabilityGraph JSON Agentia OS.
                Réponds UNIQUEMENT avec un JSON valide de la forme:
                {"schemaVersion":1,"kind":"capability-graph","meta":{"name":"...","mission":"...","domain":"..."},"nodes":[{"id":"n1","type":"trigger-webhook","label":"...","config":{}}],"edges":[{"id":"e1","from":"n1","to":"n2"}],"provider":"graph-runtime"}
                Types autorisés: trigger-*, connector-*, skill-*, action-*, decision-*, validation, stockage-bdd, notif-teams.
                """;
            var gen = await modelProvider.GenerateAsync(new ModelGenerationRequest(organizationId, message, system), cancellationToken);
            var json = ExtractJson(gen.Output);
            if (!CapabilityGraphDocument.TryParse(json, out var graph) || graph is null)
            {
                logger.LogWarning("LLM blueprint invalid — falling back to mock");
                var fallback = await mockFallback.GenerateAsync(organizationId, message, cancellationToken);
                return fallback with { ValidationNotes = "Fallback mock: JSON LLM invalide. " + fallback.ValidationNotes };
            }

            return new BlueprintResponse(
                graph.ToJson(),
                graph.Meta.Mission,
                true,
                "Blueprint généré par LLM et validé.",
                gen.EstimatedCostUsd,
                gen.PromptTokens,
                gen.CompletionTokens);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "LLM blueprint generation failed — mock fallback");
            return await mockFallback.GenerateAsync(organizationId, message, cancellationToken);
        }
    }

    public Task<BlueprintResponse> ValidateAsync(string blueprintJson, CancellationToken cancellationToken)
    {
        if (CapabilityGraphDocument.TryParse(blueprintJson, out _))
            return Task.FromResult(new BlueprintResponse(blueprintJson, "CapabilityGraph valide", true, "OK"));
        try
        {
            JsonDocument.Parse(blueprintJson);
            return Task.FromResult(new BlueprintResponse(blueprintJson, "JSON valide (legacy)", true, "Legacy blueprint JSON."));
        }
        catch (JsonException ex)
        {
            return Task.FromResult(new BlueprintResponse(blueprintJson, "Validation failed", false, ex.Message));
        }
    }

    private static string ExtractJson(string text)
    {
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start >= 0 && end > start) return text[start..(end + 1)];
        return text;
    }
}

public sealed class AuditService(AgenticFactoryDbContext db)
{
    public async Task WriteAsync(Guid organizationId, string action, string? actorEmail, string? resourceType, Guid? resourceId, object? details, CancellationToken ct)
    {
        db.AuditLogEntries.Add(new AuditLogEntry
        {
            OrganizationId = organizationId,
            Action = action,
            ActorEmail = actorEmail,
            ResourceType = resourceType,
            ResourceId = resourceId,
            DetailsJson = details is null ? "{}" : JsonSerializer.Serialize(details)
        });
        await db.SaveChangesAsync(ct);
    }
}

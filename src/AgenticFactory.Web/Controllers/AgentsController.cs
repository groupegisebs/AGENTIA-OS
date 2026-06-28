using System.Text;
using System.Text.Json;
using System.Security.Claims;
using AgenticFactory.Web.Models;
using AgenticFactory.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace AgenticFactory.Web.Controllers;

public class AgentsController(ApiClient api) : AuthenticatedController
{
    public async Task<IActionResult> Index()
    {
        SetActiveNav("Agents");
        ViewData["AgentsMode"] = true;
        AuthenticateApi(api);

        var page = await api.GetAgentsAsync();
        var vm = new AgentsIndexViewModel
        {
            UserDisplayName = User.FindFirstValue(ClaimTypes.Name) ?? User.Identity?.Name ?? "Utilisateur",
            UserRole = User.FindFirstValue(ClaimTypes.Role) ?? "Membre"
        };

        if (page is not null)
        {
            vm.Summary = new AgentsSummary(
                page.Summary.Total,
                page.Summary.Active,
                page.Summary.Running,
                page.Summary.Paused,
                page.Summary.Disabled);

            vm.Agents = page.Agents.Select(MapAgent).ToList();
            vm.WeeklyActivity = vm.Agents
                .SelectMany(a => a.RunsSparkline.Select((v, i) => (i, v)))
                .GroupBy(x => x.i)
                .OrderBy(g => g.Key)
                .Select(g => g.Sum(x => x.v))
                .ToArray();
            if (vm.WeeklyActivity.Length == 0)
                vm.WeeklyActivity = [0, 0, 0, 0, 0, 0, 0];
        }

        return View(vm);
    }

    [HttpGet]
    public IActionResult Create()
    {
        SetActiveNav("Agents");
        ViewData["AgentsMode"] = true;
        ViewData["StudioMode"] = true;
        ViewData["StudioDomains"] = StudioCatalog.Domains;
        return View(new CreateAgentViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateAgentViewModel model)
    {
        SetActiveNav("Agents");
        ViewData["AgentsMode"] = true;
        ViewData["StudioMode"] = true;
        ViewData["StudioDomains"] = StudioCatalog.Domains;

        var message = BuildCreationMessage(model);
        if (string.IsNullOrWhiteSpace(message))
        {
            ModelState.AddModelError(string.Empty, "Complétez au minimum la mission ou ajoutez une description.");
            return View(model);
        }

        AuthenticateApi(api);
        var result = await api.CreateAgentFromChatAsync(message);
        if (result is null)
        {
            ModelState.AddModelError(string.Empty, "Impossible de générer le blueprint. Vérifiez vos droits ou réessayez.");
            model.Message = message;
            return View(model);
        }

        var label = result.PromptSummary.Length > 60
            ? result.PromptSummary[..60] + "…"
            : result.PromptSummary;
        TempData["Success"] = $"Collaborateur IA « {label} » recruté. Déployez-le depuis la liste.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RequestDomain([FromBody] StudioDomainRequestModel model)
    {
        if (string.IsNullOrWhiteSpace(model.DomainName))
            return BadRequest(new { message = "Le nom du domaine est requis." });

        AuthenticateApi(api);
        var (result, error) = await api.SubmitDomainRequestAsync(
            new SubmitDomainRequestPayload(
                model.DomainName.Trim(),
                model.Industry?.Trim(),
                model.UseCase?.Trim(),
                model.Description?.Trim()));

        if (result is null)
            return StatusCode(502, new { message = error ?? "Impossible d'envoyer la demande." });

        return Ok(new { message = result.Message, id = result.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RequestObjective([FromBody] StudioObjectiveRequestModel model)
    {
        if (string.IsNullOrWhiteSpace(model.ObjectiveName))
            return BadRequest(new { message = "Le nom de l'objectif est requis." });

        AuthenticateApi(api);
        var (result, error) = await api.SubmitObjectiveRequestAsync(
            new SubmitObjectiveRequestPayload(
                model.ObjectiveName.Trim(),
                model.RelatedDomain?.Trim(),
                model.UseCase?.Trim(),
                model.Description?.Trim()));

        if (result is null)
            return StatusCode(502, new { message = error ?? "Impossible d'envoyer la demande." });

        return Ok(new { message = result.Message, id = result.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EstimateBlueprint([FromBody] StudioEstimateRequestModel model)
    {
        AuthenticateApi(api);
        var result = await api.EstimateBlueprintAsync(new StudioEstimateRequest(
            model.HasDomain,
            model.ObjectiveCount,
            model.SourceCount,
            model.ActionCount,
            model.AutonomyLevel,
            model.TriggerId,
            model.TriggerFrequency,
            model.RuntimeId,
            model.HeartbeatEnabled));

        if (result is null)
            return StatusCode(502, new { message = "Impossible de calculer l'estimation." });

        return Ok(new
        {
            complexity = result.Complexity,
            estimatedMonthlyCostUsd = result.EstimatedMonthlyCostUsd,
            aiModel = result.AiModel,
            costBasis = result.CostBasis,
            costLabel = result.CostLabel
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RecommendExecutionProvider([FromBody] RecommendExecutionProviderRequestModel model)
    {
        AuthenticateApi(api);
        var result = await api.RecommendExecutionProviderAsync(new RecommendExecutionProviderRequest(
            model.ActionId,
            model.ActuatorType ?? string.Empty,
            model.Sensors,
            model.Tools));

        if (result is null)
            return StatusCode(502, new { message = "Impossible d'obtenir une recommandation." });

        return Ok(new
        {
            providerId = result.ProviderId,
            providerName = result.ProviderName,
            providerType = result.ProviderType,
            reason = result.Reason,
            confidence = result.Confidence
        });
    }

    private static string BuildCreationMessage(CreateAgentViewModel model)
    {
        if (!string.IsNullOrWhiteSpace(model.Message))
            return model.Message.Trim();

        if (string.IsNullOrWhiteSpace(model.WizardJson))
            return string.Empty;

        try
        {
            using var doc = JsonDocument.Parse(model.WizardJson);
            var root = doc.RootElement;
            var sb = new StringBuilder();
            sb.AppendLine("Créer un agent IA Runtime Agentic via Agent Factory Studio :");

            if (root.TryGetProperty("schemaVersion", out var sv) && sv.GetInt32() >= 2)
            {
                AppendLine(sb, "Mission", root, "mission");
                AppendLine(sb, "Contexte métier", root, "missionContext");
                AppendLine(sb, "Domaine (tag)", root, "businessDomain");
                AppendArray(sb, "Capteurs (Observe)", root, "sensors");
                AppendCatalogDetails(sb, root, "sensorDetails");
                AppendArray(sb, "Compétences (Understand)", root, "skills");
                AppendCatalogDetails(sb, root, "skillDetails");
                AppendArray(sb, "Outils", root, "tools");
                AppendCatalogDetails(sb, root, "toolDetails");
                AppendArray(sb, "Actionneurs (Act)", root, "actuators");
                AppendCatalogDetails(sb, root, "actuatorDetails");
                AppendExecutionProviders(sb, root);
                AppendDecision(sb, root);
                AppendMemory(sb, root);
                AppendLine(sb, "Configuration d'exécution", root, "trigger");
                AppendLine(sb, "Runtime", root, "runtime");
                AppendExecutionDetails(sb, root);
                AppendArray(sb, "Sécurité", root, "security");
                AppendLine(sb, "Nom proposé", root, "agentName");
                AppendLine(sb, "Boucle agentique", root, "agenticLoop");
            }
            else
            {
                AppendLine(sb, "Domaine métier", root, "domain");
                AppendArray(sb, "Objectifs", root, "objectives");
                AppendArray(sb, "Sources de données", root, "sources");
                AppendCatalogDetails(sb, root, "sourceDetails");
                AppendArray(sb, "Actions workflow", root, "actions");
                AppendCatalogDetails(sb, root, "actionDetails");
                AppendLine(sb, "Configuration d'exécution", root, "trigger");
                AppendLine(sb, "Runtime", root, "runtime");
                AppendExecutionDetails(sb, root);
                AppendLine(sb, "Niveau d'autonomie", root, "autonomy");
                AppendArray(sb, "Sécurité", root, "security");
                AppendLine(sb, "Nom proposé", root, "agentName");
            }
            if (root.TryGetProperty("freeText", out var ft) && !string.IsNullOrWhiteSpace(ft.GetString()))
                sb.AppendLine($"Description complémentaire : {ft.GetString()}");
            return sb.ToString().Trim();
        }
        catch
        {
            return string.Empty;
        }
    }

    private static void AppendLine(StringBuilder sb, string label, JsonElement root, string prop)
    {
        if (root.TryGetProperty(prop, out var v) && !string.IsNullOrWhiteSpace(v.GetString()))
            sb.AppendLine($"- {label} : {v.GetString()}");
    }

    private static void AppendDecision(StringBuilder sb, JsonElement root)
    {
        if (!root.TryGetProperty("decision", out var dec) || dec.ValueKind != JsonValueKind.Object)
            return;
        var label = dec.TryGetProperty("label", out var l) ? l.GetString()
            : dec.TryGetProperty("engine", out var e) ? e.GetString() : null;
        if (!string.IsNullOrWhiteSpace(label))
            sb.AppendLine($"- Moteur de décision : {label}");
    }

    private static void AppendMemory(StringBuilder sb, JsonElement root)
    {
        if (!root.TryGetProperty("memory", out var mem) || mem.ValueKind != JsonValueKind.Object)
            return;
        if (mem.TryGetProperty("types", out var types) && types.ValueKind == JsonValueKind.Array)
        {
            var items = types.EnumerateArray().Select(x => x.GetString()).Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
            if (items.Count > 0)
                sb.AppendLine($"- Mémoire : {string.Join(", ", items)}");
        }
    }

    private static void AppendExecutionProviders(StringBuilder sb, JsonElement root)
    {
        if (root.TryGetProperty("executionProviders", out var providers) && providers.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in providers.EnumerateArray())
            {
                var label = item.TryGetProperty("actuatorLabel", out var l) ? l.GetString() : null;
                var provider = item.TryGetProperty("providerName", out var p) ? p.GetString() : null;
                if (!string.IsNullOrWhiteSpace(label) && !string.IsNullOrWhiteSpace(provider))
                    sb.AppendLine($"  · Provider d'exécution : {label} → {provider}");
            }
            return;
        }

        if (!root.TryGetProperty("actuatorDetails", out var details) || details.ValueKind != JsonValueKind.Array)
            return;

        foreach (var item in details.EnumerateArray())
        {
            var label = item.TryGetProperty("label", out var l) ? l.GetString() : null;
            if (string.IsNullOrWhiteSpace(label)) continue;
            if (item.TryGetProperty("executionProvider", out var ep) && ep.ValueKind == JsonValueKind.Object)
            {
                var name = ep.TryGetProperty("name", out var n) ? n.GetString() : null;
                if (!string.IsNullOrWhiteSpace(name))
                    sb.AppendLine($"  · Provider d'exécution : {label} → {name}");
            }
        }
    }

    private static void AppendCatalogDetails(StringBuilder sb, JsonElement root, string prop)
    {
        if (!root.TryGetProperty(prop, out var arr) || arr.ValueKind != JsonValueKind.Array)
            return;
        foreach (var item in arr.EnumerateArray())
        {
            var label = item.TryGetProperty("label", out var l) ? l.GetString() : null;
            if (string.IsNullOrWhiteSpace(label)) continue;
            if (!item.TryGetProperty("config", out var cfg) || cfg.ValueKind != JsonValueKind.Object)
            {
                sb.AppendLine($"  · {label}");
                continue;
            }
            var parts = cfg.EnumerateObject()
                .Select(p => $"{p.Name}={p.Value.GetString()}")
                .Where(x => !string.IsNullOrWhiteSpace(x));
            var detail = string.Join(", ", parts);
            sb.AppendLine(string.IsNullOrWhiteSpace(detail) ? $"  · {label}" : $"  · {label} : {detail}");
        }
    }

    private static void AppendSourceDetails(StringBuilder sb, JsonElement root) =>
        AppendCatalogDetails(sb, root, "sourceDetails");

    private static void AppendActionDetails(StringBuilder sb, JsonElement root) =>
        AppendCatalogDetails(sb, root, "actionDetails");

    private static void AppendExecutionDetails(StringBuilder sb, JsonElement root)
    {
        if (!root.TryGetProperty("execution", out var exec) || exec.ValueKind != JsonValueKind.Object)
            return;

        if (exec.TryGetProperty("resilience", out var res) && res.ValueKind == JsonValueKind.Object)
        {
            var retry = res.TryGetProperty("retryOnError", out var r) && r.GetBoolean();
            var attempts = res.TryGetProperty("maxAttempts", out var a) ? a.GetInt32() : 0;
            var wait = res.TryGetProperty("waitSeconds", out var w) ? w.GetInt32() : 0;
            sb.AppendLine($"  · Résilience : retry={(retry ? attempts + "x / " + wait + "s" : "non")}");
        }
        if (exec.TryGetProperty("logging", out var log) && log.ValueKind == JsonValueKind.Object)
        {
            var level = log.TryGetProperty("level", out var l) ? l.GetString() : null;
            var days = log.TryGetProperty("retentionDays", out var d) ? d.GetInt32() : 0;
            if (!string.IsNullOrWhiteSpace(level))
                sb.AppendLine($"  · Logs : {level}, rétention {days}j");
        }
        if (exec.TryGetProperty("supervision", out var sup) && sup.ValueKind == JsonValueKind.Object)
        {
            var hb = sup.TryGetProperty("heartbeat", out var h) && h.GetBoolean();
            sb.AppendLine($"  · Supervision : heartbeat={(hb ? "activé" : "désactivé")}");
        }
    }

    private static void AppendArray(StringBuilder sb, string label, JsonElement root, string prop)
    {
        if (!root.TryGetProperty(prop, out var arr) || arr.ValueKind != JsonValueKind.Array)
            return;
        var items = arr.EnumerateArray().Select(x => x.GetString()).Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
        if (items.Count > 0)
            sb.AppendLine($"- {label} : {string.Join(", ", items)}");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Deploy(Guid agentId, Guid blueprintId)
    {
        AuthenticateApi(api);
        var (result, error) = await api.DeployAgentAsync(agentId, blueprintId);
        if (result is null)
        {
            TempData["Error"] = string.IsNullOrWhiteSpace(error) ? "Échec du déploiement." : error;
        }
        else
        {
            TempData["Success"] = $"Agent déployé sur /api/agents/{result.EndpointSlug}/invoke";
            TempData["ApiKey"] = result.PlainApiKey;
        }
        return RedirectToAction(nameof(Index));
    }

    private static AgentListItem MapAgent(AgentListItemResponse a) => new(
        a.Id,
        a.Name,
        a.Description,
        a.EndpointSlug,
        a.Status,
        a.DisplayStatus,
        GuessCategory(a.Name, a.Description),
        a.CreatedAtUtc,
        a.LatestBlueprintId,
        a.VersionNumber.HasValue ? $"v1.{a.VersionNumber}.0" : "—",
        a.Environment,
        a.LastRunAt,
        a.RunsLast7Days,
        a.RunsLast30Days,
        a.CostLast30Days,
        a.RunsSparkline ?? []);

    private static string GuessCategory(string name, string description)
    {
        var text = $"{name} {description}".ToLowerInvariant();
        if (text.Contains("email") || text.Contains("mail")) return "Productivité";
        if (text.Contains("document") || text.Contains("pdf")) return "Documents";
        if (text.Contains("support") || text.Contains("client")) return "Support";
        if (text.Contains("data") || text.Contains("analyt")) return "Analytics";
        if (text.Contains("security") || text.Contains("sécurit")) return "Sécurité";
        return "Automation";
    }
}

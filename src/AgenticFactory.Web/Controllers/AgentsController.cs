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

        var eligibility = await api.GetPublishEligibilityAsync();
        if (eligibility is not null)
            vm.PublishEligibility = MapPublishEligibility(eligibility);

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
        var (result, apiError) = await api.CreateAgentFromChatAsync(message);
        if (result is null)
        {
            ModelState.AddModelError(string.Empty, apiError ?? "Impossible de générer le blueprint. Vérifiez vos droits ou réessayez.");
            model.Message = message;
            return View(model);
        }

        var summary = string.IsNullOrWhiteSpace(result.PromptSummary) ? message : result.PromptSummary.Trim();
        var label = summary.Length > 60 ? summary[..60] + "…" : summary;
        var costNote = result.CreationCostUsd > 0
            ? $" Coût estimé de création : ${result.CreationCostUsd:0.0000}."
            : string.Empty;
        TempData["Success"] = $"Collaborateur IA « {label} » recruté.{costNote} Déployez-le depuis la liste.";
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
            creationCostUsd = result.CreationCostUsd,
            deployFeeUsd = result.DeployFeeUsd,
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

            if (IsModernWizardSchema(root))
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

    private static bool IsModernWizardSchema(JsonElement root)
    {
        if (!root.TryGetProperty("schemaVersion", out var sv))
            return root.TryGetProperty("mission", out _);

        return sv.ValueKind switch
        {
            JsonValueKind.Number => sv.TryGetInt32(out var n) && n >= 2,
            JsonValueKind.String => int.TryParse(sv.GetString(), out var n) && n >= 2,
            _ => false
        };
    }

    private static string FormatJsonConfigValue(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString() ?? string.Empty,
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        JsonValueKind.Number => value.GetRawText(),
        JsonValueKind.Null or JsonValueKind.Undefined => string.Empty,
        _ => value.GetRawText()
    };

    private static void AppendLine(StringBuilder sb, string label, JsonElement root, string prop)
    {
        if (!root.TryGetProperty(prop, out var v))
            return;
        var text = v.ValueKind == JsonValueKind.String ? v.GetString() : FormatJsonConfigValue(v);
        if (!string.IsNullOrWhiteSpace(text))
            sb.AppendLine($"- {label} : {text}");
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
                .Select(p => $"{p.Name}={FormatJsonConfigValue(p.Value)}")
                .Where(x => !string.IsNullOrWhiteSpace(x) && !x.EndsWith('='));
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
        var (result, error, statusCode, paymentRequired) = await api.DeployAgentAsync(agentId, blueprintId);
        if (result is null)
        {
            if (statusCode == 402 && paymentRequired is not null)
            {
                TempData["Error"] = paymentRequired.Message;
                TempData["PublishPaymentAction"] = paymentRequired.CheckoutAction;
                TempData["PublishPaymentPlanId"] = paymentRequired.SubscriptionPlanId?.ToString();
                return RedirectToAction(nameof(Index));
            }
            TempData["Error"] = string.IsNullOrWhiteSpace(error) ? "Échec du déploiement." : error;
        }
        else
        {
            var feeNote = result.DeployFeeUsd > 0
                ? $" Frais de déploiement : ${result.DeployFeeUsd:0.00}."
                : string.Empty;
            TempData["Success"] = $"Agent déployé sur /api/agents/{result.EndpointSlug}/invoke.{feeNote} ({result.CurrentAgents}/{result.MaxAgents} agents)";
            TempData["ApiKey"] = result.PlainApiKey;
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PublishCheckout(string checkoutAction, Guid? planId)
    {
        AuthenticateApi(api);
        var webBase = HttpContext.Request.Scheme + "://" + HttpContext.Request.Host;
        var successUrl = $"{webBase}/Subscriptions/Success";
        var cancelUrl = $"{webBase}/Agents";

        if (string.Equals(checkoutAction, "publish_credits", StringComparison.OrdinalIgnoreCase))
        {
            var (result, error) = await api.StartConsumableCheckoutAsync("PublishCredits", successUrl, cancelUrl);
            if (error is not null)
            {
                TempData["Error"] = error;
                return RedirectToAction(nameof(Index));
            }
            if (!string.IsNullOrWhiteSpace(result?.CheckoutUrl))
                return Redirect(result.CheckoutUrl);
        }
        else if (planId is Guid id && id != Guid.Empty)
        {
            var (result, error) = await api.StartBillingCheckoutAsync(id, successUrl, cancelUrl);
            if (error is not null)
            {
                TempData["Error"] = error;
                return RedirectToAction(nameof(Index));
            }
            if (!string.IsNullOrWhiteSpace(result?.CheckoutUrl))
                return Redirect(result.CheckoutUrl);
        }

        TempData["Error"] = "Impossible de démarrer le paiement.";
        return RedirectToAction(nameof(Index));
    }

    private static PublishEligibilityInfo MapPublishEligibility(PublishEligibilityResponse e) =>
        new(e.CanPublish, e.BlockReason, e.Message, e.CtaLabel, e.CheckoutAction,
            e.RequiredAmountUsd, e.SubscriptionPlanId, e.PublishCreditsBalance, e.DeployedAgents, e.MaxAgents);

    private static AgentListItem MapAgent(AgentListItemResponse a)
    {
        var presentation = AgentIconResolver.Resolve(a.Name, a.Description);
        return new(
            a.Id,
            a.Name,
            a.Description,
            a.EndpointSlug,
            a.Status,
            a.DisplayStatus,
            presentation.Category,
            presentation.IconClass,
            presentation.IconBg,
            presentation.IconColor,
            a.CreatedAtUtc,
            a.LatestBlueprintId,
            a.VersionNumber.HasValue ? $"v1.{a.VersionNumber}.0" : "—",
            a.Environment,
            a.LastRunAt,
            a.RunsLast7Days,
            a.RunsLast30Days,
            a.CostLast30Days,
            a.RunsSparkline ?? []);
    }
}

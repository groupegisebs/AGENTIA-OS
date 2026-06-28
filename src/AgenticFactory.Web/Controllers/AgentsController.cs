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
        return View(new CreateAgentViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateAgentViewModel model)
    {
        SetActiveNav("Agents");
        ViewData["AgentsMode"] = true;
        ViewData["StudioMode"] = true;

        var message = BuildCreationMessage(model);
        if (string.IsNullOrWhiteSpace(message))
        {
            ModelState.AddModelError(string.Empty, "Complétez au minimum le domaine et un objectif, ou ajoutez une description.");
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
            sb.AppendLine("Créer un agent IA via Agent Factory Studio avec la configuration suivante :");
            AppendLine(sb, "Domaine métier", root, "domain");
            AppendArray(sb, "Objectifs", root, "objectives");
            AppendArray(sb, "Sources de données", root, "sources");
            AppendArray(sb, "Actions workflow", root, "actions");
            AppendLine(sb, "Déclencheur", root, "trigger");
            AppendLine(sb, "Runtime cible", root, "runtime");
            AppendLine(sb, "Niveau d'autonomie", root, "autonomy");
            AppendArray(sb, "Sécurité", root, "security");
            AppendLine(sb, "Nom proposé", root, "agentName");
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

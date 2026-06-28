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
        var vm = new AgentsIndexViewModel();

        if (page is not null)
        {
            vm.Summary = new AgentsSummary(
                page.Summary.Total,
                page.Summary.Active,
                page.Summary.Running,
                page.Summary.Paused,
                page.Summary.Disabled);

            vm.Agents = page.Agents.Select(MapAgent).ToList();
        }

        return View(vm);
    }

    [HttpGet]
    public IActionResult Create()
    {
        SetActiveNav("Agents");
        ViewData["AgentsMode"] = true;
        return View(new CreateAgentViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateAgentViewModel model)
    {
        SetActiveNav("Agents");
        if (string.IsNullOrWhiteSpace(model.Message))
        {
            ModelState.AddModelError(nameof(model.Message), "Décrivez ce que l'agent doit faire.");
            return View(model);
        }

        AuthenticateApi(api);
        var result = await api.CreateAgentFromChatAsync(model.Message.Trim());
        if (result is null)
        {
            ModelState.AddModelError(string.Empty, "Impossible de générer le blueprint. Vérifiez vos droits ou réessayez.");
            return View(model);
        }

        var label = result.PromptSummary.Length > 60
            ? result.PromptSummary[..60] + "…"
            : result.PromptSummary;
        TempData["Success"] = $"Agent « {label} » créé. Déployez-le depuis la liste.";
        return RedirectToAction(nameof(Index));
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
        a.CostLast30Days,
        a.RunsSparkline ?? []);

    private static string GuessCategory(string name, string description)
    {
        var text = $"{name} {description}".ToLowerInvariant();
        if (text.Contains("email") || text.Contains("mail")) return "Email";
        if (text.Contains("document") || text.Contains("pdf")) return "Documents";
        if (text.Contains("support") || text.Contains("client")) return "Support";
        if (text.Contains("data") || text.Contains("analyt")) return "Analytics";
        if (text.Contains("security") || text.Contains("sécurit")) return "Sécurité";
        return "Automation";
    }
}

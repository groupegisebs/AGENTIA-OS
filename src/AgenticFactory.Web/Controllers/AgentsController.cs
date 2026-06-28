using AgenticFactory.Web.Models;
using AgenticFactory.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace AgenticFactory.Web.Controllers;

public class AgentsController(ApiClient api) : AuthenticatedController
{
    public async Task<IActionResult> Index()
    {
        SetActiveNav("Agents");
        AuthenticateApi(api);
        var items = await api.GetAgentsAsync();
        var vm = new AgentsIndexViewModel
        {
            Agents = items?.Select(a => new AgentListItem(
                a.Id, a.Name, a.Description, a.EndpointSlug,
                a.Status, a.CreatedAtUtc, a.LatestBlueprintId)).ToList() ?? []
        };
        return View(vm);
    }

    [HttpGet]
    public IActionResult Create()
    {
        SetActiveNav("Agents");
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
}

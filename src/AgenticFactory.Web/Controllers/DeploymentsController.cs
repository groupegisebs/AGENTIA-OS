using AgenticFactory.Web.Models;
using AgenticFactory.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace AgenticFactory.Web.Controllers;

public class DeploymentsController(ApiClient api) : AuthenticatedController
{
    public async Task<IActionResult> Index()
    {
        SetActiveNav("Deployments");
        AuthenticateApi(api);
        var items = await api.GetDeploymentsAsync();
        var vm = new DeploymentsIndexViewModel
        {
            Deployments = items?.Select(d => new DeploymentListItem(
                d.Id, d.AgentId, d.AgentName, d.EndpointSlug,
                d.VersionNumber, d.Status, d.Environment,
                d.ActivatedAtUtc, d.CreatedAtUtc)).ToList() ?? []
        };
        return View(vm);
    }
}

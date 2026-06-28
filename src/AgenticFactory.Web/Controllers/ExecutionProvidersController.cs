using AgenticFactory.Web.Models;
using AgenticFactory.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace AgenticFactory.Web.Controllers;

public class ExecutionProvidersController(ApiClient api) : AuthenticatedController
{
    public async Task<IActionResult> Index()
    {
        SetActiveNav("ExecutionProviders");
        AuthenticateApi(api);
        var providers = await api.GetExecutionProvidersAsync();
        return View(new ExecutionProvidersIndexViewModel
        {
            Providers = providers?.Select(p => new ExecutionProviderListItem(
                p.Id,
                p.Name,
                p.Description,
                p.Category,
                p.ProviderType,
                p.Version,
                p.Author,
                p.State,
                p.IsEnabled,
                p.SupportsMonitoring,
                p.SupportsRetry,
                p.SupportsRollback,
                p.SupportsScheduling,
                p.SupportsParameters)).ToList() ?? []
        });
    }
}

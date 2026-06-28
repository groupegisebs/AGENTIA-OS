using AgenticFactory.Web.Models;
using AgenticFactory.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace AgenticFactory.Web.Controllers;

public class ExecutionsController(ApiClient api) : AuthenticatedController
{
    public async Task<IActionResult> Index()
    {
        SetActiveNav("Executions");
        AuthenticateApi(api);
        var items = await api.GetRunsAsync();
        var vm = new ExecutionsIndexViewModel
        {
            Runs = items?.Select(r => new RunListItem(
                r.Id, r.AgentId, r.AgentName, RunStatusLabel(r.Status),
                r.CreatedAtUtc, r.EstimatedCostUsd,
                r.PromptTokens, r.CompletionTokens, r.ErrorMessage)).ToList() ?? []
        };
        return View(vm);
    }

    private static string RunStatusLabel(int status) => status switch
    {
        1 => "Queued",
        2 => "Running",
        3 => "Completed",
        4 => "Failed",
        _ => status.ToString()
    };
}

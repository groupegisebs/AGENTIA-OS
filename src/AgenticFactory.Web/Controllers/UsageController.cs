using System.Security.Claims;
using AgenticFactory.Web.Models;
using AgenticFactory.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace AgenticFactory.Web.Controllers;

public class UsageController(ApiClient api) : AuthenticatedController
{
    public async Task<IActionResult> Index()
    {
        SetActiveNav("Usage");
        ViewData["BillingMode"] = true;
        AuthenticateApi(api);

        var vm = new UsageIndexViewModel
        {
            UserDisplayName = User.FindFirstValue(ClaimTypes.Name) ?? User.Identity?.Name ?? "Utilisateur",
            UserRole = User.FindFirstValue(ClaimTypes.Role) ?? "Membre"
        };

        var usage = await api.GetUsageAsync();
        if (usage is null)
        {
            vm.ApiAvailable = false;
            return View(vm);
        }

        vm.RunsThisMonth = usage.RunsThisMonth;
        vm.TokensThisMonth = usage.TokensThisMonth;
        vm.CostThisMonth = usage.CostThisMonth;
        vm.AiRunCostThisMonth = usage.AiRunCostThisMonth;
        vm.CreationCostThisMonth = usage.CreationCostThisMonth;
        vm.DeployCostThisMonth = usage.DeployCostThisMonth;
        vm.BlueprintsThisMonth = usage.BlueprintsThisMonth;
        vm.DeploymentsThisMonth = usage.DeploymentsThisMonth;
        vm.FailedThisMonth = usage.FailedThisMonth;
        vm.UsedRunsQuota = usage.UsedRunsQuota;
        vm.MaxRunsPerMonth = usage.MaxRunsPerMonth;
        vm.PeriodLabel = usage.PeriodLabel;
        vm.TokenSeries = usage.TokenSeries ?? [];
        vm.RunSeries = usage.RunSeries ?? [];
        vm.TotalRuns = usage.Totals.TotalRuns;
        vm.TotalTokens = usage.Totals.TotalTokens;
        vm.TotalCost = usage.Totals.TotalCost;
        vm.AiRunCostTotal = usage.Totals.AiRunCostTotal;
        vm.CreationCostTotal = usage.Totals.CreationCostTotal;
        vm.DeployCostTotal = usage.Totals.DeployCostTotal;

        return View(vm);
    }
}

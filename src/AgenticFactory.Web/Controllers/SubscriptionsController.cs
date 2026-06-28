using System.Security.Claims;
using AgenticFactory.Web.Models;
using AgenticFactory.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace AgenticFactory.Web.Controllers;

public class SubscriptionsController(ApiClient api) : AuthenticatedController
{
    public async Task<IActionResult> Index()
    {
        SetActiveNav("Subscriptions");
        ViewData["BillingMode"] = true;
        AuthenticateApi(api);

        var vm = new SubscriptionsIndexViewModel
        {
            UserDisplayName = User.FindFirstValue(ClaimTypes.Name) ?? User.Identity?.Name ?? "Utilisateur",
            UserRole = User.FindFirstValue(ClaimTypes.Role) ?? "Membre"
        };

        var subscription = await api.GetSubscriptionAsync();
        var plans = await api.GetSubscriptionPlansAsync();

        if (subscription is null && plans is null)
        {
            vm.ApiAvailable = false;
            return View(vm);
        }

        if (subscription is not null)
        {
            vm.HasSubscription = subscription.HasSubscription;
            vm.PlanName = subscription.PlanName;
            vm.MaxAgents = subscription.MaxAgents;
            vm.MaxRunsPerMonth = subscription.MaxRunsPerMonth;
            vm.MonthlyPriceUsd = subscription.MonthlyPriceUsd;
            vm.UsedRunsThisMonth = subscription.UsedRunsThisMonth;
            vm.CurrentAgents = subscription.CurrentAgents;
            vm.PeriodStartUtc = subscription.PeriodStartUtc;
            vm.PeriodEndUtc = subscription.PeriodEndUtc;
        }

        if (plans is not null)
        {
            vm.AvailablePlans = plans
                .Select(p => new SubscriptionPlanItem(
                    p.Id,
                    p.Name,
                    p.MaxAgents,
                    p.MaxRunsPerMonth,
                    p.MonthlyPriceUsd,
                    p.BlueprintCreationFeeUsd,
                    p.DeployFeeUsd,
                    string.Equals(p.Name, vm.PlanName, StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }

        return View(vm);
    }
}

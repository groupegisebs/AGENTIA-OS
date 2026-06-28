using System.Security.Claims;
using AgenticFactory.Web.Models;
using AgenticFactory.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace AgenticFactory.Web.Controllers;

public class SubscriptionsController(ApiClient api, IConfiguration configuration) : AuthenticatedController
{
    public async Task<IActionResult> Index()
    {
        SetActiveNav("Subscriptions");
        ViewData["BillingMode"] = true;
        AuthenticateApi(api);

        var vm = new SubscriptionsIndexViewModel
        {
            UserDisplayName = User.FindFirstValue(ClaimTypes.Name) ?? User.Identity?.Name ?? "Utilisateur",
            UserRole = User.FindFirstValue(ClaimTypes.Role) ?? "Membre",
            FlashMessage = TempData["FlashMessage"] as string,
            FlashError = TempData["FlashError"] as string
        };

        var paymentConfig = await api.GetBillingPaymentConfigAsync();
        if (paymentConfig is not null)
        {
            vm.PaymentConfigured = paymentConfig.IsConfigured;
            vm.PaymentMessage = paymentConfig.Message;
        }

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

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Checkout(Guid planId)
    {
        AuthenticateApi(api);

        var webBase = configuration["PublicWebBaseUrl"]?.TrimEnd('/')
            ?? $"{Request.Scheme}://{Request.Host}";

        var successUrl = $"{webBase}/Subscriptions/Success";
        var cancelUrl = $"{webBase}/Subscriptions/Cancel";

        var (result, error) = await api.StartBillingCheckoutAsync(planId, successUrl, cancelUrl);
        if (error is not null)
        {
            TempData["FlashError"] = error;
            return RedirectToAction(nameof(Index));
        }

        if (string.IsNullOrWhiteSpace(result?.CheckoutUrl))
        {
            TempData["FlashError"] = "Impossible de démarrer le paiement.";
            return RedirectToAction(nameof(Index));
        }

        return Redirect(result.CheckoutUrl);
    }

    public async Task<IActionResult> Success(Guid? checkoutId, string? paymentCode)
    {
        SetActiveNav("Subscriptions");
        ViewData["BillingMode"] = true;
        AuthenticateApi(api);

        if (checkoutId is null && string.IsNullOrWhiteSpace(paymentCode))
        {
            TempData["FlashError"] = "Identifiant de paiement manquant.";
            return RedirectToAction(nameof(Index));
        }

        var (result, error) = await api.ConfirmBillingPaymentAsync(checkoutId, paymentCode);
        if (error is not null)
        {
            TempData["FlashError"] = error;
            return RedirectToAction(nameof(Index));
        }

        TempData["FlashMessage"] = result?.Message ?? $"Abonnement {result?.PlanName} activé.";
        return RedirectToAction(nameof(Index));
    }

    public IActionResult Cancel()
    {
        TempData["FlashError"] = "Paiement annulé. Aucun changement n'a été appliqué à votre abonnement.";
        return RedirectToAction(nameof(Index));
    }
}

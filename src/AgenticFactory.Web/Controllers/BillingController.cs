using System.Security.Claims;
using AgenticFactory.Web.Models;
using AgenticFactory.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace AgenticFactory.Web.Controllers;

public class BillingController(ApiClient api) : AuthenticatedController
{
    public async Task<IActionResult> Index()
    {
        SetActiveNav("Billing");
        ViewData["BillingMode"] = true;
        AuthenticateApi(api);

        var vm = new BillingIndexViewModel
        {
            UserDisplayName = User.FindFirstValue(ClaimTypes.Name) ?? User.Identity?.Name ?? "Utilisateur",
            UserRole = User.FindFirstValue(ClaimTypes.Role) ?? "Membre"
        };

        var summary = await api.GetBillingSummaryAsync();
        if (summary is null)
        {
            vm.ApiAvailable = false;
            return View(vm);
        }

        vm.OrganizationName = summary.OrganizationName;
        vm.PlanName = summary.PlanName;
        vm.MonthlyPriceUsd = summary.MonthlyPriceUsd;
        vm.PeriodStartUtc = summary.PeriodStartUtc;
        vm.PeriodEndUtc = summary.PeriodEndUtc;
        vm.HasPaymentMethod = summary.HasPaymentMethod;
        vm.PaymentMethodLabel = summary.PaymentMethodLabel;
        vm.Invoices = (summary.Invoices ?? [])
            .Select(i => new InvoiceItem(i.Number, i.DateUtc, i.AmountUsd, i.Status))
            .ToList();

        return View(vm);
    }
}

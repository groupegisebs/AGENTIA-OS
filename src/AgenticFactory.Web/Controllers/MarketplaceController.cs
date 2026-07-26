using AgenticFactory.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace AgenticFactory.Web.Controllers;

public class MarketplaceController(ApiClient api) : AuthenticatedController
{
    public async Task<IActionResult> Index()
    {
        SetActiveNav("Marketplace");
        AuthenticateApi(api);
        var listings = await api.GetMarketplaceAsync() ?? [];
        return View(listings);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Publish(Guid agentId, decimal priceUsd, string license, string description, string category)
    {
        AuthenticateApi(api);
        var (ok, error) = await api.PublishMarketplaceAsync(agentId, priceUsd, license ?? "Mensuelle", description ?? "", category ?? "General");
        TempData[ok ? "Success" : "Error"] = ok ? "Agent publié sur le Marketplace." : error;
        return RedirectToAction(nameof(Index));
    }
}

public class ObservatoryController(ApiClient api) : AuthenticatedController
{
    public async Task<IActionResult> Index()
    {
        SetActiveNav("Observatory");
        AuthenticateApi(api);
        var snapshot = await api.GetObservatoryAsync();
        return View(snapshot);
    }
}

public class AuditController(ApiClient api) : AuthenticatedController
{
    public async Task<IActionResult> Index()
    {
        SetActiveNav("Audit");
        AuthenticateApi(api);
        var rows = await api.GetAuditAsync() ?? [];
        return View(rows);
    }
}

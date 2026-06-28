using Microsoft.AspNetCore.Mvc;

namespace AgenticFactory.Web.Controllers;

public class MarketplaceController : AuthenticatedController
{
    public IActionResult Index()
    {
        SetActiveNav("Marketplace");
        return View();
    }
}

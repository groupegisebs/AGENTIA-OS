using Microsoft.AspNetCore.Mvc;

namespace AgenticFactory.Web.Controllers;

public class ComingSoonController : AuthenticatedController
{
    public IActionResult Index(string? name)
    {
        ViewData["FeatureName"] = string.IsNullOrWhiteSpace(name) ? "Cette fonctionnalité" : name;
        return View();
    }
}

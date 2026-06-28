using AgenticFactory.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgenticFactory.Web.Controllers;

[AllowAnonymous]
public class HomeController : Controller
{
    [HttpGet]
    public IActionResult Index()
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Dashboard");

        var model = new LoginViewModel
        {
            Email = TempData["LoginEmail"] as string ?? ""
        };
        if (TempData["LoginError"] is string err)
            ModelState.AddModelError(string.Empty, err);

        return View(model);
    }
}

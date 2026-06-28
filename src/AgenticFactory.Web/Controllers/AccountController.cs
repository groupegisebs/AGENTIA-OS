using System.Security.Claims;
using AgenticFactory.Web.Models;
using AgenticFactory.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;

namespace AgenticFactory.Web.Controllers;

public class AccountController(ApiClient api) : Controller
{
    [HttpGet]
    public IActionResult Login() => View(new LoginViewModel());

    [HttpPost]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var result = await api.LoginAsync(model.Email, model.Password);
        if (result is null)
        {
            ModelState.AddModelError(string.Empty, "Email ou mot de passe incorrect.");
            return View(model);
        }

        await SignInWithCookieAsync(result);
        return RedirectToAction("Index", "Dashboard");
    }

    [HttpGet]
    public IActionResult Register() => View(new RegisterViewModel());

    [HttpPost]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var (result, error) = await api.RegisterAsync(
            model.Email, model.Password, model.DisplayName, model.OrganizationName);

        if (result is null)
        {
            ModelState.AddModelError(string.Empty, error ?? "Erreur lors de l'inscription.");
            return View(model);
        }

        await SignInWithCookieAsync(result);
        return RedirectToAction("Index", "Dashboard");
    }

    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    private async Task SignInWithCookieAsync(AuthResponse auth)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Email,            auth.Email),
            new(ClaimTypes.Name,             auth.FullName),
            new(ClaimTypes.Role,             auth.Role),
            new("OrganizationId",            auth.OrganizationId),
            new("ApiToken",                  auth.Token)
        };

        var identity  = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties { IsPersistent = false });
    }
}

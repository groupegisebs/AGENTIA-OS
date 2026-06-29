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
    public IActionResult Login()
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Dashboard");
        return RedirectToAction("Index", "Home");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
            return RedirectToHomeWithLoginModel(model);

        var result = await api.LoginAsync(model.Email, model.Password);
        if (result is null)
        {
            ModelState.AddModelError(string.Empty, "Email ou mot de passe incorrect.");
            return RedirectToHomeWithLoginModel(model);
        }

        await SignInWithCookieAsync(result);
        return RedirectToAction("Index", "Dashboard");
    }

    private IActionResult RedirectToHomeWithLoginModel(LoginViewModel model)
    {
        TempData["LoginEmail"] = model.Email;
        TempData["LoginError"] = ModelState[string.Empty]?.Errors.FirstOrDefault()?.ErrorMessage
            ?? "Veuillez corriger les champs du formulaire.";
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    public IActionResult Register() => View(new RegisterViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
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
        return RedirectToAction("Index", "Home");
    }

    private async Task SignInWithCookieAsync(AuthResponse auth)
    {
        if (string.IsNullOrWhiteSpace(auth.Token))
            throw new InvalidOperationException("Réponse d'authentification invalide (token manquant).");

        var claims = new List<Claim>
        {
            new(ClaimTypes.Email, auth.Email ?? string.Empty),
            new(ClaimTypes.Name,  auth.FullName ?? auth.Email ?? string.Empty),
            new(ClaimTypes.Role,  auth.Role ?? string.Empty),
            new("OrganizationId", auth.OrganizationId ?? string.Empty),
            new("ApiToken",       auth.Token)
        };

        var identity  = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties { IsPersistent = false });
    }
}

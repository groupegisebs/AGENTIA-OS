using AgenticFactory.Domain;
using AgenticFactory.Infrastructure.Identity;
using AgenticFactory.Infrastructure.Persistence;
using AgenticFactory.Shared;
using AgenticFactory.Web.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace AgenticFactory.Web.Controllers;

public class AccountController(
    UserManager<AppIdentityUser> userManager,
    SignInManager<AppIdentityUser> signInManager,
    AgenticFactoryDbContext dbContext) : Controller
{
    [HttpGet]
    public IActionResult Login() => View(new LoginViewModel());

    [HttpPost]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await signInManager.PasswordSignInAsync(model.Email, model.Password, false, false);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, "Invalid credentials.");
            return View(model);
        }

        return RedirectToAction("Index", "Dashboard");
    }

    [HttpGet]
    public IActionResult Register() => View(new RegisterViewModel());

    [HttpPost]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var organization = new Organization { Name = model.OrganizationName, Slug = model.OrganizationName.ToLowerInvariant().Replace(" ", "-") };
        dbContext.Organizations.Add(organization);
        await dbContext.SaveChangesAsync();

        var user = new AppIdentityUser
        {
            UserName = model.Email,
            Email = model.Email,
            DisplayName = model.DisplayName,
            OrganizationId = organization.Id,
            EmailConfirmed = true
        };
        var createResult = await userManager.CreateAsync(user, model.Password);
        if (!createResult.Succeeded)
        {
            ModelState.AddModelError(string.Empty, string.Join(", ", createResult.Errors.Select(x => x.Description)));
            return View(model);
        }

        await userManager.AddToRoleAsync(user, SystemRoles.Admin);
        dbContext.ApplicationUsers.Add(new ApplicationUser
        {
            OrganizationId = organization.Id,
            Email = model.Email,
            DisplayName = model.DisplayName,
            IdentityUserId = user.Id.ToString()
        });
        dbContext.OrganizationSubscriptions.Add(new OrganizationSubscription
        {
            OrganizationId = organization.Id,
            SubscriptionPlanId = dbContext.SubscriptionPlans.First().Id,
            IsActive = true
        });
        await dbContext.SaveChangesAsync();
        await signInManager.SignInAsync(user, false);
        return RedirectToAction("Index", "Dashboard");
    }

    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        await signInManager.SignOutAsync();
        return RedirectToAction(nameof(Login));
    }
}

using AgenticFactory.Application;
using AgenticFactory.Domain;
using AgenticFactory.Infrastructure.Identity;
using AgenticFactory.Infrastructure.Persistence;
using AgenticFactory.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AgenticFactory.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(
    UserManager<AppIdentityUser> userManager,
    AgenticFactoryDbContext dbContext,
    IJwtTokenService tokenService) : ControllerBase
{
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        var baseSlug = string.Join("-", request.OrganizationName
            .ToLowerInvariant()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        if (string.IsNullOrWhiteSpace(baseSlug))
            baseSlug = "org";

        var slug = baseSlug;
        var suffix = 0;
        while (await dbContext.Organizations.AnyAsync(x => x.Slug == slug, cancellationToken))
        {
            suffix++;
            slug = $"{baseSlug}-{suffix}";
        }

        var starterPlan = await dbContext.SubscriptionPlans
            .OrderBy(x => x.MonthlyPriceUsd)
            .FirstOrDefaultAsync(cancellationToken);
        if (starterPlan is null)
        {
            starterPlan = new SubscriptionPlan
            {
                Name = "Starter",
                MaxAgents = 5,
                MaxRunsPerMonth = 5000,
                MonthlyPriceUsd = 99,
                PublishModel = PublishModel.SubscriptionIncluded
            };
            dbContext.SubscriptionPlans.Add(starterPlan);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var organization = new Organization
        {
            Name = request.OrganizationName,
            Slug = slug
        };
        dbContext.Organizations.Add(organization);
        await dbContext.SaveChangesAsync(cancellationToken);

        var user = new AppIdentityUser
        {
            UserName = request.Email,
            Email = request.Email,
            DisplayName = request.DisplayName,
            OrganizationId = organization.Id,
            EmailConfirmed = true
        };

        var create = await userManager.CreateAsync(user, request.Password);
        if (!create.Succeeded)
        {
            dbContext.Organizations.Remove(organization);
            await dbContext.SaveChangesAsync(cancellationToken);
            return BadRequest(new { message = string.Join(" ", create.Errors.Select(x => x.Description)) });
        }

        await userManager.AddToRoleAsync(user, SystemRoles.Creator);
        dbContext.ApplicationUsers.Add(new ApplicationUser
        {
            OrganizationId = organization.Id,
            Email = request.Email,
            DisplayName = request.DisplayName,
            IdentityUserId = user.Id.ToString()
        });
        await dbContext.OrganizationSubscriptions.AddAsync(new OrganizationSubscription
        {
            OrganizationId = organization.Id,
            SubscriptionPlanId = starterPlan.Id,
            IsActive = true
        }, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        var roles = await userManager.GetRolesAsync(user);
        var token = tokenService.CreateToken(user.Id, user.OrganizationId, user.Email ?? request.Email, roles);
        return Ok(BuildAuthResponse(token, user, roles));
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null || !await userManager.CheckPasswordAsync(user, request.Password))
        {
            return Unauthorized();
        }

        var roles = await userManager.GetRolesAsync(user);
        var token = tokenService.CreateToken(user.Id, user.OrganizationId, user.Email ?? request.Email, roles);
        return Ok(BuildAuthResponse(token, user, roles));
    }

    private static object BuildAuthResponse(string token, AppIdentityUser user, IList<string> roles) => new
    {
        accessToken = token,
        email = user.Email ?? user.UserName ?? string.Empty,
        fullName = user.DisplayName,
        organizationId = user.OrganizationId,
        role = roles.FirstOrDefault() ?? SystemRoles.Viewer
    };
}

public sealed record RegisterRequest(string OrganizationName, string Email, string DisplayName, string Password);
public sealed record LoginRequest(string Email, string Password);

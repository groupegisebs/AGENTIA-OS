using AgenticFactory.Application;
using AgenticFactory.Domain;
using AgenticFactory.Infrastructure.Identity;
using AgenticFactory.Infrastructure.Persistence;
using AgenticFactory.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

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
        var organization = new Organization
        {
            Name = request.OrganizationName,
            Slug = request.OrganizationName.ToLowerInvariant().Replace(" ", "-")
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
            return BadRequest(create.Errors.Select(x => x.Description));
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
            SubscriptionPlanId = dbContext.SubscriptionPlans.First().Id,
            IsActive = true
        }, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new { organizationId = organization.Id, userId = user.Id });
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
        return Ok(new { accessToken = token, organizationId = user.OrganizationId, roles });
    }
}

public sealed record RegisterRequest(string OrganizationName, string Email, string DisplayName, string Password);
public sealed record LoginRequest(string Email, string Password);

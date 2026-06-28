using AgenticFactory.Application;
using AgenticFactory.Domain;
using AgenticFactory.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AgenticFactory.Api.Controllers;

[ApiController]
[Route("api/studio")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class StudioController(
    AgenticFactoryDbContext db,
    ICurrentTenantService tenantService,
    ILogger<StudioController> logger) : ControllerBase
{
    [HttpPost("domain-requests")]
    [Authorize(Policy = "CanCreateAgent")]
    public async Task<IActionResult> SubmitDomainRequest(
        SubmitDomainRequestDto request,
        CancellationToken cancellationToken)
    {
        var organizationId = tenantService.OrganizationId;
        if (organizationId == Guid.Empty)
            return BadRequest(new { message = "Contexte organisation manquant." });

        if (string.IsNullOrWhiteSpace(request.DomainName))
            return BadRequest(new { message = "Le nom du domaine est requis." });

        var email = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value
            ?? User.Identity?.Name
            ?? "unknown@agentia.local";
        var name = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value
            ?? User.FindFirst("full_name")?.Value
            ?? email;

        var entity = new StudioDomainRequest
        {
            OrganizationId = organizationId,
            RequestedByEmail = email,
            RequestedByName = name,
            DomainName = request.DomainName.Trim(),
            Industry = request.Industry?.Trim(),
            UseCase = request.UseCase?.Trim(),
            Description = request.Description?.Trim(),
            Status = DomainRequestStatus.Pending
        };

        db.StudioDomainRequests.Add(entity);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Domain request {RequestId} submitted by {Email} for « {DomainName} » (org {OrgId})",
            entity.Id, email, entity.DomainName, organizationId);

        return Ok(new
        {
            id = entity.Id,
            status = entity.Status.ToString(),
            message = "Votre demande a été transmise à l'équipe Agentia."
        });
    }

    [HttpGet("domain-requests")]
    [Authorize(Policy = "CanCreateAgent")]
    public async Task<IActionResult> ListDomainRequests(CancellationToken cancellationToken)
    {
        var organizationId = tenantService.OrganizationId;
        if (organizationId == Guid.Empty)
            return BadRequest(new { message = "Contexte organisation manquant." });

        var items = await db.StudioDomainRequests
            .AsNoTracking()
            .Where(x => x.OrganizationId == organizationId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(50)
            .Select(x => new
            {
                x.Id,
                x.DomainName,
                x.Industry,
                x.Status,
                x.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    [HttpPost("objective-requests")]
    [Authorize(Policy = "CanCreateAgent")]
    public async Task<IActionResult> SubmitObjectiveRequest(
        SubmitObjectiveRequestDto request,
        CancellationToken cancellationToken)
    {
        var organizationId = tenantService.OrganizationId;
        if (organizationId == Guid.Empty)
            return BadRequest(new { message = "Contexte organisation manquant." });

        if (string.IsNullOrWhiteSpace(request.ObjectiveName))
            return BadRequest(new { message = "Le nom de l'objectif est requis." });

        var email = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value
            ?? User.Identity?.Name
            ?? "unknown@agentia.local";
        var name = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value
            ?? User.FindFirst("full_name")?.Value
            ?? email;

        var entity = new StudioObjectiveRequest
        {
            OrganizationId = organizationId,
            RequestedByEmail = email,
            RequestedByName = name,
            ObjectiveName = request.ObjectiveName.Trim(),
            RelatedDomain = request.RelatedDomain?.Trim(),
            UseCase = request.UseCase?.Trim(),
            Description = request.Description?.Trim(),
            Status = DomainRequestStatus.Pending
        };

        db.StudioObjectiveRequests.Add(entity);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Objective request {RequestId} submitted by {Email} for « {ObjectiveName} » (org {OrgId})",
            entity.Id, email, entity.ObjectiveName, organizationId);

        return Ok(new
        {
            id = entity.Id,
            status = entity.Status.ToString(),
            message = "Votre demande d'objectif a été transmise à l'équipe Agentia."
        });
    }
}

public sealed record SubmitDomainRequestDto(
    string DomainName,
    string? Industry,
    string? UseCase,
    string? Description);

public sealed record SubmitObjectiveRequestDto(
    string ObjectiveName,
    string? RelatedDomain,
    string? UseCase,
    string? Description);

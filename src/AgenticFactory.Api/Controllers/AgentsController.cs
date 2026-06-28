using AgenticFactory.Application;
using AgenticFactory.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AgenticFactory.Api.Controllers;

[ApiController]
[Route("api/agents")]
public class AgentsController(
    ICurrentTenantService tenantService,
    IAgentDeploymentService deploymentService,
    IAgentInvocationService invocationService,
    AgenticFactoryDbContext dbContext) : ControllerBase
{
    [HttpGet]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "CanViewDashboard")]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var organizationId = tenantService.OrganizationId;
        if (organizationId == Guid.Empty)
            return BadRequest("Missing organization context.");

        var agents = await dbContext.Agents
            .Where(x => x.OrganizationId == organizationId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.Description,
                x.EndpointSlug,
                status = x.Status.ToString(),
                x.CreatedAtUtc,
                latestBlueprintId = dbContext.AgentBlueprints
                    .Where(b => b.AgentId == x.Id && b.OrganizationId == organizationId)
                    .OrderByDescending(b => b.CreatedAtUtc)
                    .Select(b => (Guid?)b.Id)
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        return Ok(agents);
    }

    [HttpGet("deployments")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "CanViewDashboard")]
    public async Task<IActionResult> ListDeployments(CancellationToken cancellationToken)
    {
        var organizationId = tenantService.OrganizationId;
        if (organizationId == Guid.Empty)
            return BadRequest("Missing organization context.");

        var deployments = await dbContext.AgentDeployments
            .Where(x => x.OrganizationId == organizationId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => new
            {
                x.Id,
                x.AgentId,
                agentName = x.Agent!.Name,
                endpointSlug = x.Agent!.EndpointSlug,
                versionNumber = x.AgentVersion!.VersionNumber,
                status = x.Status.ToString(),
                x.Environment,
                x.ActivatedAtUtc,
                x.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);

        return Ok(deployments);
    }

    [HttpPost("deploy")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "CanCreateAgent")]
    public async Task<IActionResult> Deploy(DeployAgentRequest request, CancellationToken cancellationToken)
    {
        var organizationId = tenantService.OrganizationId;
        if (organizationId == Guid.Empty)
        {
            return BadRequest("Missing organization context.");
        }

        var result = await deploymentService.DeployAsync(organizationId, request, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{endpointSlug}/invoke")]
    [AllowAnonymous]
    public async Task<IActionResult> Invoke(string endpointSlug, InvokeAgentRequest request, CancellationToken cancellationToken)
    {
        var organizationId = tenantService.OrganizationId;
        if (organizationId == Guid.Empty)
        {
            return BadRequest("Missing organization context.");
        }

        var apiKey = Request.Headers["X-Agent-Key"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return Unauthorized("Missing X-Agent-Key header.");
        }

        try
        {
            var result = await invocationService.InvokeAsync(organizationId, endpointSlug, apiKey, request, cancellationToken);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ex.Message);
        }
    }
}

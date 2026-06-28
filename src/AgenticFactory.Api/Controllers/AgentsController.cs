using AgenticFactory.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgenticFactory.Api.Controllers;

[ApiController]
[Route("api/agents")]
public class AgentsController(
    ICurrentTenantService tenantService,
    IAgentDeploymentService deploymentService,
    IAgentInvocationService invocationService) : ControllerBase
{
    [HttpPost("deploy")]
    [Authorize(Policy = "CanCreateAgent")]
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
        var apiKey = Request.Headers["X-Agent-Key"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return Unauthorized("Missing X-Agent-Key header.");
        }

        var result = await invocationService.InvokeAsync(endpointSlug, apiKey, request, cancellationToken);
        return Ok(result);
    }
}

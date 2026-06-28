using AgenticFactory.Application;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgenticFactory.Api.Controllers;

[ApiController]
[Route("api/agent-creation")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "CanCreateAgent")]
public class AgentCreationController(
    ICurrentTenantService tenantService,
    IAgentCreationService creationService) : ControllerBase
{
    [HttpPost("chat")]
    public async Task<IActionResult> CreateFromChat(ChatMessageRequest request, CancellationToken cancellationToken)
    {
        var organizationId = tenantService.OrganizationId;
        if (organizationId == Guid.Empty)
        {
            return BadRequest("Missing organization context.");
        }

        var blueprint = await creationService.CreateBlueprintFromChatAsync(organizationId, request, cancellationToken);
        return Ok(blueprint);
    }

    [HttpPost("validate")]
    public async Task<IActionResult> ValidateBlueprint(ValidateBlueprintRequest request, CancellationToken cancellationToken)
    {
        var result = await creationService.ValidateBlueprintAsync(request.BlueprintJson, cancellationToken);
        return Ok(result);
    }
}

public sealed record ValidateBlueprintRequest(string BlueprintJson);

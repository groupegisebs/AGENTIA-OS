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
        if (string.IsNullOrWhiteSpace(request.Message))
            return BadRequest(new { message = "Message is required." });

        var organizationId = tenantService.OrganizationId;
        if (organizationId == Guid.Empty)
            return BadRequest(new { message = "Missing organization context." });

        try
        {
            var blueprint = await creationService.CreateBlueprintFromChatAsync(organizationId, request, cancellationToken);
            return Ok(new BlueprintCreatedResponse(
                blueprint.Id,
                blueprint.AgentId,
                blueprint.PromptSummary,
                blueprint.BlueprintJson,
                blueprint.Status.ToString(),
                blueprint.ValidationNotes));
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Blueprint generation failed.", detail = ex.Message });
        }
    }

    [HttpPost("validate")]
    public async Task<IActionResult> ValidateBlueprint(ValidateBlueprintRequest request, CancellationToken cancellationToken)
    {
        var result = await creationService.ValidateBlueprintAsync(request.BlueprintJson, cancellationToken);
        return Ok(result);
    }
}

public sealed record ValidateBlueprintRequest(string BlueprintJson);

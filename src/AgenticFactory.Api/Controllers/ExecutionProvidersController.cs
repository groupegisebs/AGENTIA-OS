using AgenticFactory.Application;
using AgenticFactory.Domain;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgenticFactory.Api.Controllers;

[ApiController]
[Route("api/execution-providers")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class ExecutionProvidersController(
    IExecutionProviderCatalogService catalogService,
    IExecutionProviderRecommendationService recommendationService) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = "CanViewDashboard")]
    public async Task<IActionResult> List([FromQuery] bool enabledOnly = true, CancellationToken cancellationToken = default)
    {
        var items = await catalogService.ListAsync(enabledOnly, cancellationToken);
        return Ok(items.Select(MapProvider));
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "CanViewDashboard")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var provider = await catalogService.GetByIdAsync(id, cancellationToken);
        return provider is null ? NotFound() : Ok(MapProvider(provider));
    }

    [HttpPost("recommend")]
    [Authorize(Policy = "CanCreateAgent")]
    public async Task<IActionResult> Recommend(
        [FromBody] RecommendProviderRequestDto request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ActuatorType))
            return BadRequest(new { message = "Le type d'actionneur est requis." });

        var recommendation = await recommendationService.RecommendProviderAsync(
            new RecommendProviderRequest(
                request.ActionId,
                request.ActuatorType,
                request.Sensors ?? [],
                request.Tools ?? []),
            cancellationToken);

        if (recommendation is null)
            return NotFound(new { message = "Aucun provider disponible." });

        return Ok(new
        {
            providerId = recommendation.ProviderId,
            providerName = recommendation.ProviderName,
            providerType = recommendation.ProviderType.ToString(),
            reason = recommendation.Reason,
            confidence = recommendation.Confidence
        });
    }

    [HttpPost("{id:guid}/generate")]
    [Authorize(Policy = "CanCreateAgent")]
    public async Task<IActionResult> Generate(
        Guid id,
        [FromBody] GenerateFlowRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await catalogService.GeneratePreviewAsync(
            id,
            new GenerateFlowRequest(
                request.ActuatorType ?? "action",
                request.ActionLabel ?? "Action",
                request.Parameters ?? new Dictionary<string, object?>(),
                request.Configuration),
            cancellationToken);

        return result is null
            ? NotFound(new { message = "Provider introuvable ou génération non supportée." })
            : Ok(new { result.ProviderType, result.Format, contentJson = result.ContentJson });
    }

    private static object MapProvider(ActionExecutionProvider p) => new
    {
        p.Id,
        p.Name,
        p.Description,
        p.Category,
        providerType = p.ProviderType.ToString(),
        p.IsSystem,
        p.SupportsParameters,
        p.SupportsMonitoring,
        p.SupportsRetry,
        p.SupportsRollback,
        p.SupportsScheduling,
        p.Version,
        p.Author,
        isEnabled = p.IsEnabled,
        state = p.IsEnabled ? "Actif" : "Désactivé"
    };
}

public sealed record RecommendProviderRequestDto(
    string? ActionId,
    string ActuatorType,
    IReadOnlyList<string>? Sensors,
    IReadOnlyList<string>? Tools);

public sealed record GenerateFlowRequestDto(
    string? ActuatorType,
    string? ActionLabel,
    Dictionary<string, object?>? Parameters,
    Dictionary<string, object?>? Configuration);

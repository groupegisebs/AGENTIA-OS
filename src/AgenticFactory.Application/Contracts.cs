using AgenticFactory.Domain;

namespace AgenticFactory.Application;

public sealed record ChatMessageRequest(string Message, Guid? ExistingAgentId);
public sealed record BlueprintResponse(string BlueprintJson, string Summary, bool IsValid, string ValidationNotes);
public sealed record BlueprintCreatedResponse(
    Guid Id,
    Guid AgentId,
    string PromptSummary,
    string BlueprintJson,
    string Status,
    string ValidationNotes);
public sealed record DeployAgentRequest(Guid AgentId, Guid BlueprintId, string Environment);
public sealed record DeployAgentResponse(Guid AgentId, Guid AgentVersionId, Guid DeploymentId, string EndpointSlug, string PlainApiKey);
public sealed record InvokeAgentRequest(Dictionary<string, object?> Input);
public sealed record InvokeAgentResponse(Guid RunId, string Status, Dictionary<string, object?> Output, int PromptTokens, int CompletionTokens, decimal EstimatedCostUsd);
public sealed record ModelGenerationRequest(Guid OrganizationId, string Prompt, string? SystemPrompt);
public sealed record ModelGenerationResult(
    string Output,
    int PromptTokens,
    int CompletionTokens,
    decimal EstimatedCostUsd,
    string Provider,
    bool UsedFallback);

public interface IBlueprintGenerator
{
    Task<BlueprintResponse> GenerateAsync(Guid organizationId, string message, CancellationToken cancellationToken);
    Task<BlueprintResponse> ValidateAsync(string blueprintJson, CancellationToken cancellationToken);
}

public interface IAgentCreationService
{
    Task<AgentBlueprint> CreateBlueprintFromChatAsync(Guid organizationId, ChatMessageRequest request, CancellationToken cancellationToken);
    Task<BlueprintResponse> ValidateBlueprintAsync(string blueprintJson, CancellationToken cancellationToken);
}

public interface IAgentDeploymentService
{
    Task<DeployAgentResponse> DeployAsync(Guid organizationId, DeployAgentRequest request, CancellationToken cancellationToken);
}

public interface IAgentInvocationService
{
    Task<InvokeAgentResponse> InvokeAsync(Guid organizationId, string endpointSlug, string apiKey, InvokeAgentRequest request, CancellationToken cancellationToken);
}

public interface ICurrentTenantService
{
    Guid OrganizationId { get; }
}

public interface IJwtTokenService
{
    string CreateToken(Guid userId, Guid organizationId, string email, IEnumerable<string> roles);
}

public interface IAgentRuntime
{
    Task TickAsync(CancellationToken cancellationToken);
}

public interface IAgentExecutor
{
    Task<InvokeAgentResponse> ExecuteAsync(Guid organizationId, Agent agent, AgentVersion version, Dictionary<string, object?> input, CancellationToken cancellationToken);
}

public interface IAgentToolExecutor
{
    Task<Dictionary<string, object?>> ExecuteToolsAsync(Guid organizationId, Agent agent, Dictionary<string, object?> input, CancellationToken cancellationToken);
}

public interface IAgentMemoryService
{
    Task RememberAsync(Guid runId, string data, CancellationToken cancellationToken);
}

public interface IAgentModelProvider
{
    Task<ModelGenerationResult> GenerateAsync(ModelGenerationRequest request, CancellationToken cancellationToken);
}

using AgenticFactory.Domain;

namespace AgenticFactory.Application;

public sealed record ExecutionRequest(
    Guid OrganizationId,
    Guid? AgentId,
    string ActionType,
    string ActionLabel,
    Dictionary<string, object?> Parameters,
    string? ConfigurationJson,
    int TimeoutSeconds,
    CancellationToken CancellationToken = default);

public sealed record ExecutionResult(
    bool Success,
    string Status,
    string? OutputJson,
    string? ErrorMessage,
    int DurationMs,
    int RetryCount,
    Guid? ProviderId,
    string ProviderType);

public sealed record ExecutionValidationResult(bool IsValid, IReadOnlyList<string> Errors);

public sealed record ProviderRecommendation(
    Guid ProviderId,
    string ProviderName,
    ExecutionProviderType ProviderType,
    string Reason,
    double Confidence);

public sealed record RecommendProviderRequest(
    string? ActionId,
    string ActuatorType,
    IReadOnlyList<string> Sensors,
    IReadOnlyList<string> Tools);

public sealed record GenerateFlowRequest(
    string ActuatorType,
    string ActionLabel,
    Dictionary<string, object?> Parameters,
    Dictionary<string, object?>? Configuration);

public sealed record GeneratedFlowResult(
    string ProviderType,
    string Format,
    string ContentJson);

public interface IExecutionProvider
{
    ExecutionProviderType ProviderType { get; }
    Task<ExecutionValidationResult> ValidateAsync(ExecutionRequest request, CancellationToken cancellationToken);
    Task<ExecutionResult> ExecuteAsync(ExecutionRequest request, CancellationToken cancellationToken);
    Task<ExecutionResult> DeployAsync(ExecutionRequest request, CancellationToken cancellationToken);
    Task<ExecutionResult> MonitorAsync(Guid executionId, CancellationToken cancellationToken);
    Task<ExecutionResult> RollbackAsync(Guid executionId, CancellationToken cancellationToken);
    Task<ExecutionValidationResult> HealthCheckAsync(CancellationToken cancellationToken);
}

public interface IExecutionProviderRegistry
{
    IReadOnlyList<IExecutionProvider> GetAll();
    IExecutionProvider? GetByType(ExecutionProviderType providerType);
    IExecutionProvider? GetById(Guid providerId);
}

public interface IExecutionProviderRecommendationService
{
    Task<ProviderRecommendation?> RecommendProviderAsync(
        RecommendProviderRequest request,
        CancellationToken cancellationToken);
}

public interface IPowerAutomateGenerator
{
    GeneratedFlowResult GenerateFlow(GenerateFlowRequest request);
    GeneratedFlowResult GenerateTrigger(GenerateFlowRequest request);
    GeneratedFlowResult GenerateActions(GenerateFlowRequest request);
    GeneratedFlowResult GenerateExpressions(GenerateFlowRequest request);
    GeneratedFlowResult GenerateVariables(GenerateFlowRequest request);
}

public interface ILogicAppGenerator
{
    GeneratedFlowResult GenerateWorkflow(GenerateFlowRequest request);
}

public interface IN8nWorkflowGenerator
{
    GeneratedFlowResult GenerateWorkflow(GenerateFlowRequest request);
}

public interface IExecutionProviderCatalogService
{
    Task<IReadOnlyList<ActionExecutionProvider>> ListAsync(bool enabledOnly, CancellationToken cancellationToken);
    Task<ActionExecutionProvider?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<GeneratedFlowResult?> GeneratePreviewAsync(Guid providerId, GenerateFlowRequest request, CancellationToken cancellationToken);
}

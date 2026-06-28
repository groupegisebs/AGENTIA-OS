using System.Text.Json;
using AgenticFactory.Application;
using AgenticFactory.Domain;

namespace AgenticFactory.Infrastructure.Services.ExecutionProviders;

public sealed class WindowsRuntimeProvider : IExecutionProvider
{
    public ExecutionProviderType ProviderType => ExecutionProviderType.InternalRuntime;

    public Task<ExecutionValidationResult> ValidateAsync(ExecutionRequest request, CancellationToken cancellationToken)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(request.ActionType))
            errors.Add("ActionType is required.");
        return Task.FromResult(new ExecutionValidationResult(errors.Count == 0, errors));
    }

    public async Task<ExecutionResult> ExecuteAsync(ExecutionRequest request, CancellationToken cancellationToken)
    {
        var validation = await ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return new ExecutionResult(
                false, "ValidationFailed", null,
                string.Join("; ", validation.Errors), 0, 0, null, ProviderType.ToString());
        }

        var started = DateTime.UtcNow;
        await Task.Delay(10, cancellationToken);
        var output = JsonSerializer.Serialize(new
        {
            provider = ProviderType.ToString(),
            action = request.ActionType,
            label = request.ActionLabel,
            status = "completed",
            executedAtUtc = DateTime.UtcNow
        });

        return new ExecutionResult(
            true, "Completed", output, null,
            (int)(DateTime.UtcNow - started).TotalMilliseconds, 0, null, ProviderType.ToString());
    }

    public Task<ExecutionResult> DeployAsync(ExecutionRequest request, CancellationToken cancellationToken)
        => ExecuteAsync(request, cancellationToken);

    public Task<ExecutionResult> MonitorAsync(Guid executionId, CancellationToken cancellationToken)
        => Task.FromResult(new ExecutionResult(true, "Healthy", "{}", null, 0, 0, null, ProviderType.ToString()));

    public Task<ExecutionResult> RollbackAsync(Guid executionId, CancellationToken cancellationToken)
        => Task.FromResult(new ExecutionResult(true, "RolledBack", "{}", null, 0, 0, null, ProviderType.ToString()));

    public Task<ExecutionValidationResult> HealthCheckAsync(CancellationToken cancellationToken)
        => Task.FromResult(new ExecutionValidationResult(true, []));
}

public sealed class StubExecutionProvider(ExecutionProviderType providerType) : IExecutionProvider
{
    public ExecutionProviderType ProviderType => providerType;

    public Task<ExecutionValidationResult> ValidateAsync(ExecutionRequest request, CancellationToken cancellationToken)
        => Task.FromResult(new ExecutionValidationResult(true, []));

    public Task<ExecutionResult> ExecuteAsync(ExecutionRequest request, CancellationToken cancellationToken)
        => Task.FromResult(new ExecutionResult(
            true, "Stubbed", $"{{\"provider\":\"{providerType}\"}}", null, 0, 0, null, providerType.ToString()));

    public Task<ExecutionResult> DeployAsync(ExecutionRequest request, CancellationToken cancellationToken)
        => ExecuteAsync(request, cancellationToken);

    public Task<ExecutionResult> MonitorAsync(Guid executionId, CancellationToken cancellationToken)
        => Task.FromResult(new ExecutionResult(true, "Stubbed", "{}", null, 0, 0, null, providerType.ToString()));

    public Task<ExecutionResult> RollbackAsync(Guid executionId, CancellationToken cancellationToken)
        => Task.FromResult(new ExecutionResult(true, "Stubbed", "{}", null, 0, 0, null, providerType.ToString()));

    public Task<ExecutionValidationResult> HealthCheckAsync(CancellationToken cancellationToken)
        => Task.FromResult(new ExecutionValidationResult(true, []));
}

public sealed class ExecutionProviderRegistry(IEnumerable<IExecutionProvider> providers) : IExecutionProviderRegistry
{
    private readonly IReadOnlyList<IExecutionProvider> _providers = providers.ToList();

    public IReadOnlyList<IExecutionProvider> GetAll() => _providers;

    public IExecutionProvider? GetByType(ExecutionProviderType providerType)
        => _providers.FirstOrDefault(p => p.ProviderType == providerType);

    public IExecutionProvider? GetById(Guid providerId)
        => ExecutionProviderSeed.TryGetType(providerId, out var type)
            ? GetByType(type)
            : null;
}

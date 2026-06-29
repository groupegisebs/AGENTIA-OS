using System.IdentityModel.Tokens.Jwt;
using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Collections.Concurrent;
using System.Net.Http.Json;
using AgenticFactory.Application;
using AgenticFactory.Domain;
using AgenticFactory.Infrastructure.Identity;
using AgenticFactory.Infrastructure.Billing;
using AgenticFactory.Infrastructure.Persistence;
using AgenticFactory.Infrastructure.Services.ExecutionProviders;
using AgenticFactory.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace AgenticFactory.Infrastructure.Services;

public sealed class CurrentTenantService(IHttpContextAccessor accessor) : ICurrentTenantService
{
    public Guid OrganizationId
    {
        get
        {
            var claim = accessor.HttpContext?.User.FindFirstValue("org_id")
                ?? accessor.HttpContext?.Request.Headers["X-Organization-Id"].FirstOrDefault();

            return Guid.TryParse(claim, out var id) ? id : Guid.Empty;
        }
    }
}

public static class TenantGuard
{
    public static void RequireOrganization(Guid organizationId)
    {
        if (organizationId == Guid.Empty)
        {
            throw new UnauthorizedAccessException("Missing organization context.");
        }
    }

    public static void EnsureMatch(Guid expectedOrganizationId, Guid resourceOrganizationId, string resourceName)
    {
        if (expectedOrganizationId != resourceOrganizationId)
        {
            throw new UnauthorizedAccessException($"{resourceName} does not belong to the current organization.");
        }
    }
}

public sealed class MockBlueprintGenerator(IConfiguration configuration) : IBlueprintGenerator
{
    public Task<BlueprintResponse> GenerateAsync(Guid organizationId, string message, CancellationToken cancellationToken)
    {
        var mode = configuration["AI:Mode"] ?? "mock";
        var metadata = AgentMessageParser.Parse(message);
        var executionProviders = ExtractExecutionProviders(message);
        var definition = new
        {
            provider = mode,
            name = metadata.Name,
            domain = metadata.DomainId,
            category = metadata.DomainLabel,
            orchestrator = "WindowsService",
            steps = new[]
            {
                "Parse incoming message",
                "Call model provider",
                "Execute configured tools",
                "Return structured output"
            },
            actions = executionProviders.Select(p => new
            {
                p.Label,
                p.ActuatorType,
                executionProvider = p.ProviderName,
                providerType = p.ProviderType,
                p.ExecutionMode,
                p.TimeoutSeconds
            }).ToArray(),
            executionProviders,
            fallback = "mock-workflow"
        };

        var json = JsonSerializer.Serialize(definition, new JsonSerializerOptions { WriteIndented = true });
        var estimate = AiPricingHelper.EstimateFromPrompt(message, configuration);
        return Task.FromResult(new BlueprintResponse(
            json,
            metadata.Description,
            true,
            "Blueprint appears valid in mock mode.",
            estimate.EstimatedCostUsd,
            estimate.PromptTokens,
            estimate.CompletionTokens));
    }

    private static List<BlueprintActionProvider> ExtractExecutionProviders(string message)
    {
        var results = new List<BlueprintActionProvider>();
        if (string.IsNullOrWhiteSpace(message))
            return results;

        foreach (var line in message.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!line.Contains("Provider d'exécution", StringComparison.OrdinalIgnoreCase)
                && !line.Contains("Execution provider", StringComparison.OrdinalIgnoreCase))
                continue;

            var parts = line.Split(':', 2);
            if (parts.Length < 2) continue;
            var detail = parts[1].Trim();
            var label = detail;
            var provider = "Windows Runtime";
            var providerType = "InternalRuntime";
            if (detail.Contains('→'))
            {
                var seg = detail.Split('→', 2);
                label = seg[0].Trim();
                provider = seg[1].Trim();
                providerType = MapProviderName(provider);
            }

            results.Add(new BlueprintActionProvider(label, label, provider, providerType, "Synchronous", 300));
        }

        return results;
    }

    private static string MapProviderName(string name) => name switch
    {
        var n when n.Contains("Power Automate", StringComparison.OrdinalIgnoreCase) => "PowerAutomate",
        var n when n.Contains("Logic Apps", StringComparison.OrdinalIgnoreCase) => "LogicApps",
        var n when n.Contains("n8n", StringComparison.OrdinalIgnoreCase) => "N8n",
        var n when n.Contains("Webhook", StringComparison.OrdinalIgnoreCase) => "Webhook",
        var n when n.Contains("REST", StringComparison.OrdinalIgnoreCase) => "RestApi",
        var n when n.Contains("PowerShell", StringComparison.OrdinalIgnoreCase) => "PowerShell",
        var n when n.Contains("Python", StringComparison.OrdinalIgnoreCase) => "Python",
        var n when n.Contains("Docker", StringComparison.OrdinalIgnoreCase) => "DockerJob",
        var n when n.Contains("Azure Function", StringComparison.OrdinalIgnoreCase) => "AzureFunction",
        _ => "InternalRuntime"
    };

    private sealed record BlueprintActionProvider(
        string Label,
        string ActuatorType,
        string ProviderName,
        string ProviderType,
        string ExecutionMode,
        int TimeoutSeconds);

    public Task<BlueprintResponse> ValidateAsync(string blueprintJson, CancellationToken cancellationToken)
    {
        try
        {
            JsonDocument.Parse(blueprintJson);
            return Task.FromResult(new BlueprintResponse(blueprintJson, "Validation complete", true, "Valid JSON blueprint."));
        }
        catch (JsonException ex)
        {
            return Task.FromResult(new BlueprintResponse(blueprintJson, "Validation failed", false, ex.Message));
        }
    }
}

public sealed class AgentCreationService(
    AgenticFactoryDbContext dbContext,
    IBlueprintGenerator blueprintGenerator,
    IAgentPayGatewayProductService agentPayGatewayProduct) : IAgentCreationService
{
    public async Task<AgentBlueprint> CreateBlueprintFromChatAsync(Guid organizationId, ChatMessageRequest request, CancellationToken cancellationToken)
    {
        TenantGuard.RequireOrganization(organizationId);
        var generated = await blueprintGenerator.GenerateAsync(organizationId, request.Message, cancellationToken);
        var metadata = AgentMessageParser.Parse(request.Message);

        var planFee = await dbContext.OrganizationSubscriptions
            .AsNoTracking()
            .Where(x => x.OrganizationId == organizationId && x.IsActive)
            .Select(x => x.SubscriptionPlan!.BlueprintCreationFeeUsd)
            .FirstOrDefaultAsync(cancellationToken);

        var creationCostUsd = Math.Round(generated.EstimatedCostUsd + planFee, 6);

        var agent = request.ExistingAgentId.HasValue
            ? await dbContext.Agents.FirstAsync(x => x.Id == request.ExistingAgentId.Value && x.OrganizationId == organizationId, cancellationToken)
            : new Agent
            {
                OrganizationId = organizationId,
                Name = metadata.Name,
                Description = string.IsNullOrWhiteSpace(generated.Summary) ? metadata.Description : generated.Summary,
                EndpointSlug = $"agent-{Guid.NewGuid():N}"[..18],
                Status = AgentStatus.Draft
            };

        if (!request.ExistingAgentId.HasValue)
        {
            dbContext.Agents.Add(agent);

            var productCode = await agentPayGatewayProduct.TryEnsureAgentProductAsync(
                agent, organizationId, cancellationToken);
            if (productCode is not null)
            {
                agent.PayGatewayProductCode = productCode;
            }
        }

        var blueprint = new AgentBlueprint
        {
            OrganizationId = organizationId,
            AgentId = agent.Id,
            PromptSummary = request.Message,
            BlueprintJson = generated.BlueprintJson,
            Status = generated.IsValid ? BlueprintStatus.Validated : BlueprintStatus.Rejected,
            ValidationNotes = generated.ValidationNotes,
            PromptTokens = generated.PromptTokens,
            CompletionTokens = generated.CompletionTokens,
            CreationCostUsd = creationCostUsd
        };

        dbContext.AgentBlueprints.Add(blueprint);
        await dbContext.SaveChangesAsync(cancellationToken);
        return blueprint;
    }

    public Task<BlueprintResponse> ValidateBlueprintAsync(string blueprintJson, CancellationToken cancellationToken)
        => blueprintGenerator.ValidateAsync(blueprintJson, cancellationToken);
}

public sealed class AgentDeploymentService(
    AgenticFactoryDbContext dbContext,
    IPublishEligibilityService publishEligibility) : IAgentDeploymentService
{
    public async Task<DeployAgentResponse> DeployAsync(Guid organizationId, DeployAgentRequest request, CancellationToken cancellationToken)
    {
        TenantGuard.RequireOrganization(organizationId);

        var eligibility = await publishEligibility.EvaluateAsync(organizationId, request.AgentId, cancellationToken);
        if (!eligibility.CanPublish)
        {
            throw new PublishPaymentRequiredException(eligibility);
        }

        var subscription = await dbContext.OrganizationSubscriptions
            .Include(x => x.SubscriptionPlan)
            .FirstOrDefaultAsync(x => x.OrganizationId == organizationId && x.IsActive, cancellationToken)
            ?? throw new InvalidOperationException("No active subscription.");

        var deployedAgents = await dbContext.Agents.CountAsync(
            x => x.OrganizationId == organizationId && x.Status == AgentStatus.Active, cancellationToken);

        if (eligibility.ConsumesPublishCredit)
        {
            subscription.PublishCredits -= 1;
            subscription.UpdatedAtUtc = DateTime.UtcNow;
        }

        var deployFeeUsd = subscription.SubscriptionPlan!.DeployFeeUsd;

        var agent = await dbContext.Agents.FirstAsync(x => x.Id == request.AgentId && x.OrganizationId == organizationId, cancellationToken);
        var blueprint = await dbContext.AgentBlueprints.FirstAsync(x => x.Id == request.BlueprintId && x.OrganizationId == organizationId, cancellationToken);

        var nextVersion = await dbContext.AgentVersions
            .Where(x => x.AgentId == agent.Id && x.OrganizationId == organizationId)
            .Select(x => (int?)x.VersionNumber)
            .MaxAsync(cancellationToken) ?? 0;

        var version = new AgentVersion
        {
            OrganizationId = organizationId,
            AgentId = agent.Id,
            VersionNumber = nextVersion + 1,
            DefinitionJson = blueprint.BlueprintJson,
            IsActive = true
        };
        dbContext.AgentVersions.Add(version);

        var apiKey = $"ak_{Guid.NewGuid():N}";
        var deployment = new AgentDeployment
        {
            OrganizationId = organizationId,
            AgentId = agent.Id,
            AgentVersion = version,
            Environment = request.Environment,
            ApiKeyHash = ApiKeyHasher.Hash(apiKey),
            Status = DeploymentStatus.Active,
            DeployFeeUsd = deployFeeUsd,
            ActivatedAtUtc = DateTime.UtcNow
        };
        dbContext.AgentDeployments.Add(deployment);

        agent.ActiveVersionId = version.Id;
        agent.Status = AgentStatus.Active;
        await dbContext.SaveChangesAsync(cancellationToken);

        return new DeployAgentResponse(
            agent.Id,
            version.Id,
            deployment.Id,
            agent.EndpointSlug,
            apiKey,
            deployFeeUsd,
            deployedAgents + (agent.Status == AgentStatus.Active ? 0 : 1),
            subscription.SubscriptionPlan.MaxAgents);
    }
}

public static class ApiKeyHasher
{
    public static string Hash(string apiKey)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(apiKey));
        return Convert.ToHexString(bytes);
    }
}

public sealed class AgentInvocationService(
    AgenticFactoryDbContext dbContext,
    IAgentExecutor executor,
    IHubContext<RunStatusHub> runHub) : IAgentInvocationService
{
    public async Task<InvokeAgentResponse> InvokeAsync(Guid organizationId, string endpointSlug, string apiKey, InvokeAgentRequest request, CancellationToken cancellationToken)
    {
        TenantGuard.RequireOrganization(organizationId);
        var agent = await dbContext.Agents
            .FirstOrDefaultAsync(x => x.EndpointSlug == endpointSlug && x.Status == AgentStatus.Active, cancellationToken)
            ?? throw new InvalidOperationException("Agent not found.");
        TenantGuard.EnsureMatch(organizationId, agent.OrganizationId, "Agent");

        var deployment = await dbContext.AgentDeployments
            .Include(x => x.AgentVersion)
            .FirstOrDefaultAsync(x => x.AgentId == agent.Id && x.Status == DeploymentStatus.Active && x.OrganizationId == organizationId, cancellationToken)
            ?? throw new InvalidOperationException("No active deployment.");

        if (!string.Equals(deployment.ApiKeyHash, ApiKeyHasher.Hash(apiKey), StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException("Invalid API key.");
        }

        var version = deployment.AgentVersion ?? throw new InvalidOperationException("Missing deployed version.");
        var response = await executor.ExecuteAsync(agent.OrganizationId, agent, version, request.Input, cancellationToken);
        await runHub.Clients.All.SendAsync("run-status", response.RunId, response.Status, cancellationToken);
        return response;
    }
}

public sealed class AgentExecutor(
    AgenticFactoryDbContext dbContext,
    IAgentToolExecutor toolExecutor,
    IAgentMemoryService memoryService,
    IAgentModelProvider modelProvider) : IAgentExecutor
{
    public async Task<InvokeAgentResponse> ExecuteAsync(Guid organizationId, Agent agent, AgentVersion version, Dictionary<string, object?> input, CancellationToken cancellationToken)
    {
        TenantGuard.RequireOrganization(organizationId);
        TenantGuard.EnsureMatch(organizationId, agent.OrganizationId, "Agent");
        TenantGuard.EnsureMatch(organizationId, version.OrganizationId, "Agent version");

        // Future: persist ActionExecutionLog entries per action (Provider, Duration, Status, Error, Retry).
        var run = new AgentRun
        {
            OrganizationId = organizationId,
            AgentId = agent.Id,
            AgentVersionId = version.Id,
            Status = RunStatus.Running,
            InputJson = JsonSerializer.Serialize(input),
            StartedAtUtc = DateTime.UtcNow
        };
        dbContext.AgentRuns.Add(run);
        await dbContext.SaveChangesAsync(cancellationToken);

        var subscription = await dbContext.OrganizationSubscriptions
            .Include(x => x.SubscriptionPlan)
            .FirstOrDefaultAsync(x => x.OrganizationId == organizationId && x.IsActive, cancellationToken);

        if (subscription?.SubscriptionPlan is not null)
        {
            AiPricingHelper.ResetBillingPeriodIfNeeded(subscription);
            if (subscription.UsedRunsThisMonth >= subscription.SubscriptionPlan.MaxRunsPerMonth)
            {
                if (subscription.ConsumableRunsBalance > 0)
                {
                    subscription.ConsumableRunsBalance -= 1;
                }
                else
                {
                    throw new InvalidOperationException("Quota de runs mensuel dépassé. Achetez un pack de runs.");
                }
            }
        }

        try
        {
            var toolOutput = await toolExecutor.ExecuteToolsAsync(organizationId, agent, input, cancellationToken);
            var prompt = $"Agent: {agent.Name}\nInput:{JsonSerializer.Serialize(input)}\nTools:{JsonSerializer.Serialize(toolOutput)}";
            var generation = await modelProvider.GenerateAsync(new ModelGenerationRequest(organizationId, prompt, null), cancellationToken);
            var combinedOutput = new Dictionary<string, object?>(toolOutput)
            {
                ["modelResponse"] = generation.Output,
                ["modelProvider"] = generation.Provider,
                ["usedFallback"] = generation.UsedFallback
            };

            run.Status = RunStatus.Completed;
            run.OutputJson = JsonSerializer.Serialize(combinedOutput);
            run.PromptTokens = generation.PromptTokens;
            run.CompletionTokens = generation.CompletionTokens;
            run.EstimatedCostUsd = generation.EstimatedCostUsd;
            run.CompletedAtUtc = DateTime.UtcNow;
            await memoryService.RememberAsync(run.Id, run.OutputJson, cancellationToken);

            if (subscription is not null)
            {
                subscription.UsedRunsThisMonth += 1;
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            return new InvokeAgentResponse(run.Id, run.Status.ToString(), combinedOutput, generation.PromptTokens, generation.CompletionTokens, generation.EstimatedCostUsd);
        }
        catch (Exception ex)
        {
            run.Status = RunStatus.Failed;
            run.ErrorMessage = ex.Message;
            run.CompletedAtUtc = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
            throw;
        }
    }
}

public sealed class AgentToolExecutor : IAgentToolExecutor
{
    public Task<Dictionary<string, object?>> ExecuteToolsAsync(Guid organizationId, Agent agent, Dictionary<string, object?> input, CancellationToken cancellationToken)
        => Task.FromResult(new Dictionary<string, object?>
        {
            ["toolCount"] = 1,
            ["normalizedInput"] = input,
            ["organizationId"] = organizationId.ToString()
        });
}

public sealed class AgentMemoryService : IAgentMemoryService
{
    public Task RememberAsync(Guid runId, string data, CancellationToken cancellationToken) => Task.CompletedTask;
}

public sealed class AgentModelProvider(
    IConfiguration configuration,
    IHttpClientFactory httpClientFactory,
    ILogger<AgentModelProvider> logger) : IAgentModelProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<ModelGenerationResult> GenerateAsync(ModelGenerationRequest request, CancellationToken cancellationToken)
    {
        TenantGuard.RequireOrganization(request.OrganizationId);

        var mode = (configuration["AI:Mode"] ?? "mock").ToLowerInvariant();
        if (string.Equals(mode, "mock", StringComparison.OrdinalIgnoreCase))
        {
            return GenerateMock(request.Prompt, "mock", usedFallback: false);
        }

        var provider = (configuration["AI:Provider"] ?? mode).ToLowerInvariant();
        try
        {
            return provider switch
            {
                "openai" => await CallOpenAiAsync(request, cancellationToken),
                "azureopenai" => await CallAzureOpenAiAsync(request, cancellationToken),
                _ => GenerateMock(request.Prompt, "mock", usedFallback: true)
            };
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "AI provider failed for organization {OrganizationId}. Falling back to mock.", request.OrganizationId);
            return GenerateMock(request.Prompt, "mock-fallback", usedFallback: true);
        }
    }

    private async Task<ModelGenerationResult> CallOpenAiAsync(ModelGenerationRequest request, CancellationToken cancellationToken)
    {
        var apiKey = configuration["AI:OpenAI:ApiKey"] ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        var model = configuration["AI:OpenAI:Model"] ?? "gpt-4o-mini";
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            logger.LogInformation("OpenAI API key is missing. Using mock fallback.");
            return GenerateMock(request.Prompt, "mock-fallback", usedFallback: true);
        }

        var endpoint = configuration["AI:OpenAI:Endpoint"] ?? "https://api.openai.com/v1/chat/completions";
        var body = new
        {
            model,
            messages = new object[]
            {
                new { role = "system", content = request.SystemPrompt ?? "You are a concise enterprise agent runtime model." },
                new { role = "user", content = request.Prompt }
            },
            temperature = 0.2
        };

        var httpClient = httpClientFactory.CreateClient();
        using var message = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = JsonContent.Create(body, options: JsonOptions)
        };
        message.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
        return await SendAndParseResponseAsync(httpClient, message, "openai", cancellationToken);
    }

    private async Task<ModelGenerationResult> CallAzureOpenAiAsync(ModelGenerationRequest request, CancellationToken cancellationToken)
    {
        var apiKey = configuration["AI:AzureOpenAI:ApiKey"] ?? Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY");
        var endpoint = configuration["AI:AzureOpenAI:Endpoint"];
        var deployment = configuration["AI:AzureOpenAI:Deployment"];
        var apiVersion = configuration["AI:AzureOpenAI:ApiVersion"] ?? "2024-10-21";
        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(deployment))
        {
            logger.LogInformation("Azure OpenAI configuration is incomplete. Using mock fallback.");
            return GenerateMock(request.Prompt, "mock-fallback", usedFallback: true);
        }

        var endpointUri = $"{endpoint.TrimEnd('/')}/openai/deployments/{deployment}/chat/completions?api-version={apiVersion}";
        var body = new
        {
            messages = new object[]
            {
                new { role = "system", content = request.SystemPrompt ?? "You are a concise enterprise agent runtime model." },
                new { role = "user", content = request.Prompt }
            },
            temperature = 0.2
        };

        var httpClient = httpClientFactory.CreateClient();
        using var message = new HttpRequestMessage(HttpMethod.Post, endpointUri)
        {
            Content = JsonContent.Create(body, options: JsonOptions)
        };
        message.Headers.Add("api-key", apiKey);
        return await SendAndParseResponseAsync(httpClient, message, "azureopenai", cancellationToken);
    }

    private async Task<ModelGenerationResult> SendAndParseResponseAsync(HttpClient client, HttpRequestMessage request, string provider, CancellationToken cancellationToken)
    {
        using var response = await client.SendAsync(request, cancellationToken);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Provider {provider} returned {(int)response.StatusCode}: {payload}");
        }

        using var json = JsonDocument.Parse(payload);
        var root = json.RootElement;
        var output = root.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? string.Empty;
        var promptTokens = ReadInt(root, "usage", "prompt_tokens");
        var completionTokens = ReadInt(root, "usage", "completion_tokens");
        if (promptTokens == 0)
        {
            promptTokens = Math.Max(30, output.Length / 5);
        }
        if (completionTokens == 0)
        {
            completionTokens = Math.Max(20, output.Length / 6);
        }

        var cost = AiPricingHelper.EstimateCostUsd(promptTokens, completionTokens, configuration);
        return new ModelGenerationResult(output, promptTokens, completionTokens, cost, provider, UsedFallback: false);
    }

    private int ReadInt(JsonElement root, string parent, string child)
    {
        if (!root.TryGetProperty(parent, out var parentProperty) || !parentProperty.TryGetProperty(child, out var value))
        {
            return 0;
        }

        return value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var intValue)
            ? intValue
            : 0;
    }

    private ModelGenerationResult GenerateMock(string prompt, string provider, bool usedFallback)
    {
        var estimate = AiPricingHelper.EstimateFromPrompt(prompt, configuration);
        return new ModelGenerationResult(
            $"mock-response::{prompt[..Math.Min(40, prompt.Length)]}",
            estimate.PromptTokens,
            estimate.CompletionTokens,
            estimate.EstimatedCostUsd,
            provider,
            usedFallback);
    }
}

public sealed class JwtTokenService(IConfiguration configuration) : IJwtTokenService
{
    public string CreateToken(Guid userId, Guid organizationId, string email, IEnumerable<string> roles)
    {
        var key = configuration["Auth:JwtKey"] ?? "change-this-development-key-min-32-characters";
        var issuer = configuration["Auth:Issuer"] ?? "AgenticFactory";
        var audience = configuration["Auth:Audience"] ?? "AgenticFactoryClients";

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Email, email),
            new("org_id", organizationId.ToString())
        };
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(issuer, audience, claims, expires: DateTime.UtcNow.AddHours(8), signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

public sealed class RuntimeEngine(
    AgenticFactoryDbContext dbContext,
    IAgentExecutor executor,
    ILogger<RuntimeEngine> logger) : IAgentRuntime
{
    private static readonly ConcurrentDictionary<Guid, byte> RunningAgents = new();

    public async Task TickAsync(CancellationToken cancellationToken)
    {
        var utcNow = DateTime.UtcNow;
        var triggers = await dbContext.AgentTriggers
            .Include(x => x.Agent)
            .Where(x => x.IsEnabled
                && x.Agent != null
                && x.Agent.Status == AgentStatus.Active
                && (x.Type == TriggerType.Interval || x.Type == TriggerType.Scheduled))
            .ToListAsync(cancellationToken);

        var executedCount = 0;
        var failedCount = 0;

        foreach (var trigger in triggers)
        {
            if (trigger.Agent is null || !ShouldRunTrigger(trigger, utcNow))
            {
                continue;
            }

            if (!RunningAgents.TryAdd(trigger.AgentId, 1))
            {
                logger.LogInformation(
                    "Skipping trigger {TriggerId} for agent {AgentId}; run already in progress.",
                    trigger.Id,
                    trigger.AgentId);
                continue;
            }

            try
            {
                var deployment = await dbContext.AgentDeployments
                    .Include(x => x.AgentVersion)
                    .Where(x => x.AgentId == trigger.AgentId
                        && x.OrganizationId == trigger.OrganizationId
                        && x.Status == DeploymentStatus.Active)
                    .OrderByDescending(x => x.ActivatedAtUtc)
                    .FirstOrDefaultAsync(cancellationToken);

                if (deployment?.AgentVersion is null)
                {
                    logger.LogWarning(
                        "Skipping trigger {TriggerId}; no active deployment for agent {AgentId}.",
                        trigger.Id,
                        trigger.AgentId);
                    continue;
                }

                var runtimeInput = new Dictionary<string, object?>
                {
                    ["source"] = "runtime-scheduler",
                    ["triggerId"] = trigger.Id,
                    ["triggerType"] = trigger.Type.ToString(),
                    ["scheduledAtUtc"] = utcNow
                };

                logger.LogInformation(
                    "Executing trigger {TriggerId} for agent {AgentId} in org {OrganizationId}.",
                    trigger.Id,
                    trigger.AgentId,
                    trigger.OrganizationId);

                await executor.ExecuteAsync(trigger.OrganizationId, trigger.Agent, deployment.AgentVersion, runtimeInput, cancellationToken);
                trigger.LastTriggeredAtUtc = utcNow;
                executedCount++;
            }
            catch (Exception ex)
            {
                failedCount++;
                logger.LogError(
                    ex,
                    "Runtime trigger execution failed for trigger {TriggerId}, agent {AgentId}, org {OrganizationId}.",
                    trigger.Id,
                    trigger.AgentId,
                    trigger.OrganizationId);
            }
            finally
            {
                RunningAgents.TryRemove(trigger.AgentId, out _);
            }
        }

        await UpdateHeartbeatAsync(triggers.Count, failedCount, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Runtime tick complete. ActiveTriggers={ActiveTriggers} Executed={Executed} Failed={Failed}.",
            triggers.Count,
            executedCount,
            failedCount);
    }

    private async Task UpdateHeartbeatAsync(int activeTriggerCount, int failedCount, CancellationToken cancellationToken)
    {
        var heartbeat = await dbContext.RuntimeHeartbeats.FirstOrDefaultAsync(x => x.NodeName == Environment.MachineName, cancellationToken);
        if (heartbeat is null)
        {
            heartbeat = new RuntimeHeartbeat
            {
                NodeName = Environment.MachineName,
                Status = failedCount > 0 ? "Degraded" : "Healthy",
                ActiveTriggerCount = activeTriggerCount,
                LastSeenUtc = DateTime.UtcNow
            };
            dbContext.RuntimeHeartbeats.Add(heartbeat);
            return;
        }

        heartbeat.Status = failedCount > 0 ? "Degraded" : "Healthy";
        heartbeat.LastSeenUtc = DateTime.UtcNow;
        heartbeat.ActiveTriggerCount = activeTriggerCount;
    }

    private static bool ShouldRunTrigger(AgentTrigger trigger, DateTime utcNow)
    {
        return trigger.Type switch
        {
            TriggerType.Interval => ShouldRunInterval(trigger.CronOrInterval, trigger.LastTriggeredAtUtc, utcNow),
            TriggerType.Scheduled => ShouldRunCron(trigger.CronOrInterval, trigger.LastTriggeredAtUtc, utcNow),
            _ => false
        };
    }

    private static bool ShouldRunInterval(string expression, DateTime? lastTriggeredAtUtc, DateTime utcNow)
    {
        if (!TryParseInterval(expression, out var interval))
        {
            interval = TimeSpan.FromMinutes(1);
        }

        return !lastTriggeredAtUtc.HasValue || (utcNow - lastTriggeredAtUtc.Value) >= interval;
    }

    private static bool TryParseInterval(string expression, out TimeSpan interval)
    {
        if (TimeSpan.TryParse(expression, out interval))
        {
            return interval > TimeSpan.Zero;
        }

        var input = expression.Trim().ToLowerInvariant();
        if (input.Length < 2 || !int.TryParse(input[..^1], out var value) || value <= 0)
        {
            interval = TimeSpan.Zero;
            return false;
        }

        interval = input[^1] switch
        {
            's' => TimeSpan.FromSeconds(value),
            'm' => TimeSpan.FromMinutes(value),
            'h' => TimeSpan.FromHours(value),
            'd' => TimeSpan.FromDays(value),
            _ => TimeSpan.Zero
        };

        return interval > TimeSpan.Zero;
    }

    private static bool ShouldRunCron(string cronExpression, DateTime? lastTriggeredAtUtc, DateTime utcNow)
    {
        if (!TryMatchBasicCron(cronExpression, utcNow))
        {
            return false;
        }

        return !lastTriggeredAtUtc.HasValue
            || lastTriggeredAtUtc.Value.Year != utcNow.Year
            || lastTriggeredAtUtc.Value.DayOfYear != utcNow.DayOfYear
            || lastTriggeredAtUtc.Value.Hour != utcNow.Hour
            || lastTriggeredAtUtc.Value.Minute != utcNow.Minute;
    }

    private static bool TryMatchBasicCron(string cronExpression, DateTime utcNow)
    {
        var parts = cronExpression.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 5)
        {
            return false;
        }

        return MatchCronPart(parts[0], utcNow.Minute, 0, 59)
            && MatchCronPart(parts[1], utcNow.Hour, 0, 23)
            && MatchCronPart(parts[2], utcNow.Day, 1, 31)
            && MatchCronPart(parts[3], utcNow.Month, 1, 12)
            && MatchCronPart(parts[4], (int)utcNow.DayOfWeek, 0, 6);
    }

    private static bool MatchCronPart(string part, int value, int min, int max)
    {
        if (part == "*")
        {
            return true;
        }

        if (part.StartsWith("*/", StringComparison.Ordinal) && int.TryParse(part[2..], out var step) && step > 0)
        {
            return value % step == 0;
        }

        return int.TryParse(part, out var fixedValue) && fixedValue >= min && fixedValue <= max && value == fixedValue;
    }
}

public class RunStatusHub : Hub;

public sealed class IdentitySeedService(
    AgenticFactoryDbContext dbContext,
    UserManager<AppIdentityUser> userManager,
    RoleManager<IdentityRole<Guid>> roleManager,
    Billing.GisebsPayGatewayCatalogSync payGatewayCatalogSync)
{
    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        if (dbContext.Database.IsRelational())
        {
            await dbContext.Database.MigrateAsync(cancellationToken);
        }
        else
        {
            await dbContext.Database.EnsureCreatedAsync(cancellationToken);
        }

        foreach (var role in new[] { SystemRoles.Admin, SystemRoles.Creator, SystemRoles.Viewer })
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole<Guid>(role));
            }
        }

        var organization = await dbContext.Organizations.FirstOrDefaultAsync(cancellationToken);
        if (organization is null)
        {
            organization = new Organization { Name = "Default Org", Slug = "default-org" };
            dbContext.Organizations.Add(organization);
            dbContext.SubscriptionPlans.Add(new SubscriptionPlan
            {
                Name = "Starter",
                MaxAgents = 5,
                MaxRunsPerMonth = 5000,
                MonthlyPriceUsd = 99,
                PublishModel = PublishModel.SubscriptionIncluded
            });
            dbContext.SubscriptionPlans.Add(new SubscriptionPlan
            {
                Name = "Pro",
                MaxAgents = 25,
                MaxRunsPerMonth = 50000,
                MonthlyPriceUsd = 299,
                BlueprintCreationFeeUsd = 0,
                DeployFeeUsd = 0,
                PublishModel = PublishModel.SubscriptionIncluded
            });
            dbContext.SubscriptionPlans.Add(new SubscriptionPlan
            {
                Name = "Enterprise",
                MaxAgents = 50,
                MaxRunsPerMonth = 500000,
                MonthlyPriceUsd = 999,
                BlueprintCreationFeeUsd = 0,
                DeployFeeUsd = 0,
                PublishModel = PublishModel.ConsumableExtra,
                PublishCreditPriceUsd = 49m,
                PublishCreditPackSize = 1,
                RunPackPriceUsd = 29m,
                RunPackSize = 5000
            });
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        if (!await dbContext.SubscriptionPlans.AnyAsync(x => x.Name == "Pro", cancellationToken))
        {
            dbContext.SubscriptionPlans.Add(new SubscriptionPlan
            {
                Name = "Pro",
                MaxAgents = 100,
                MaxRunsPerMonth = 50000,
                MonthlyPriceUsd = 299
            });
            dbContext.SubscriptionPlans.Add(new SubscriptionPlan
            {
                Name = "Enterprise",
                MaxAgents = 500,
                MaxRunsPerMonth = 500000,
                MonthlyPriceUsd = 999
            });
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var starterPlan = await dbContext.SubscriptionPlans.FirstAsync(x => x.Name == "Starter", cancellationToken);
        if (!await dbContext.OrganizationSubscriptions.AnyAsync(x => x.OrganizationId == organization.Id && x.IsActive, cancellationToken))
        {
            dbContext.OrganizationSubscriptions.Add(new OrganizationSubscription
            {
                OrganizationId = organization.Id,
                SubscriptionPlanId = starterPlan.Id,
                IsActive = true
            });
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var adminEmail = "admin@agenticfactory.local";
        var existingAdmin = await userManager.FindByEmailAsync(adminEmail);
        if (existingAdmin is null)
        {
            var identityUser = new AppIdentityUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                OrganizationId = organization.Id,
                DisplayName = "Admin User",
                EmailConfirmed = true
            };

            var createResult = await userManager.CreateAsync(identityUser, "Admin123$ChangeMe");
            if (!createResult.Succeeded)
            {
                throw new InvalidOperationException($"Admin seed failed: {string.Join(", ", createResult.Errors.Select(x => x.Description))}");
            }

            await userManager.AddToRolesAsync(identityUser, [SystemRoles.Admin, SystemRoles.Creator]);
            dbContext.ApplicationUsers.Add(new ApplicationUser
            {
                OrganizationId = organization.Id,
                Email = adminEmail,
                DisplayName = "Admin User",
                IdentityUserId = identityUser.Id.ToString()
            });
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        await ExecutionProviderSeed.SeedAsync(dbContext, cancellationToken);
        await payGatewayCatalogSync.TrySyncSubscriptionPlansAsync(cancellationToken);
    }
}

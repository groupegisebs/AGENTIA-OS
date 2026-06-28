using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AgenticFactory.Application;
using AgenticFactory.Domain;
using AgenticFactory.Infrastructure.Identity;
using AgenticFactory.Infrastructure.Persistence;
using AgenticFactory.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
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

public sealed class MockBlueprintGenerator(IConfiguration configuration) : IBlueprintGenerator
{
    public Task<BlueprintResponse> GenerateAsync(Guid organizationId, string message, CancellationToken cancellationToken)
    {
        var mode = configuration["AI:Mode"] ?? "mock";
        var definition = new
        {
            provider = mode,
            name = "Chat Generated Agent",
            steps = new[]
            {
                "Parse incoming message",
                "Call model provider",
                "Execute configured tools",
                "Return structured output"
            },
            fallback = "mock-workflow"
        };

        var json = JsonSerializer.Serialize(definition, new JsonSerializerOptions { WriteIndented = true });
        return Task.FromResult(new BlueprintResponse(json, $"Blueprint generated from '{message}'", true, "Blueprint appears valid in mock mode."));
    }

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
    IBlueprintGenerator blueprintGenerator) : IAgentCreationService
{
    public async Task<AgentBlueprint> CreateBlueprintFromChatAsync(Guid organizationId, ChatMessageRequest request, CancellationToken cancellationToken)
    {
        var generated = await blueprintGenerator.GenerateAsync(organizationId, request.Message, cancellationToken);
        var agent = request.ExistingAgentId.HasValue
            ? await dbContext.Agents.FirstAsync(x => x.Id == request.ExistingAgentId.Value && x.OrganizationId == organizationId, cancellationToken)
            : new Agent
            {
                OrganizationId = organizationId,
                Name = $"Agent {DateTime.UtcNow:yyyyMMddHHmmss}",
                Description = generated.Summary,
                EndpointSlug = $"agent-{Guid.NewGuid():N}"[..18],
                Status = AgentStatus.Draft
            };

        if (!request.ExistingAgentId.HasValue)
        {
            dbContext.Agents.Add(agent);
        }

        var blueprint = new AgentBlueprint
        {
            OrganizationId = organizationId,
            AgentId = agent.Id,
            PromptSummary = request.Message,
            BlueprintJson = generated.BlueprintJson,
            Status = generated.IsValid ? BlueprintStatus.Validated : BlueprintStatus.Rejected,
            ValidationNotes = generated.ValidationNotes
        };

        dbContext.AgentBlueprints.Add(blueprint);
        await dbContext.SaveChangesAsync(cancellationToken);
        return blueprint;
    }

    public Task<BlueprintResponse> ValidateBlueprintAsync(string blueprintJson, CancellationToken cancellationToken)
        => blueprintGenerator.ValidateAsync(blueprintJson, cancellationToken);
}

public sealed class AgentDeploymentService(
    AgenticFactoryDbContext dbContext) : IAgentDeploymentService
{
    public async Task<DeployAgentResponse> DeployAsync(Guid organizationId, DeployAgentRequest request, CancellationToken cancellationToken)
    {
        var subscription = await dbContext.OrganizationSubscriptions
            .Include(x => x.SubscriptionPlan)
            .FirstOrDefaultAsync(x => x.OrganizationId == organizationId && x.IsActive, cancellationToken)
            ?? throw new InvalidOperationException("No active subscription.");

        var currentAgents = await dbContext.Agents.CountAsync(x => x.OrganizationId == organizationId, cancellationToken);
        if (currentAgents > subscription.SubscriptionPlan!.MaxAgents)
        {
            throw new InvalidOperationException("Agent quota exceeded for this plan.");
        }

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
            ActivatedAtUtc = DateTime.UtcNow
        };
        dbContext.AgentDeployments.Add(deployment);

        agent.ActiveVersionId = version.Id;
        agent.Status = AgentStatus.Active;
        await dbContext.SaveChangesAsync(cancellationToken);

        return new DeployAgentResponse(agent.Id, version.Id, deployment.Id, agent.EndpointSlug, apiKey);
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
    public async Task<InvokeAgentResponse> InvokeAsync(string endpointSlug, string apiKey, InvokeAgentRequest request, CancellationToken cancellationToken)
    {
        var agent = await dbContext.Agents
            .FirstOrDefaultAsync(x => x.EndpointSlug == endpointSlug && x.Status == AgentStatus.Active, cancellationToken)
            ?? throw new InvalidOperationException("Agent not found.");

        var deployment = await dbContext.AgentDeployments
            .Include(x => x.AgentVersion)
            .FirstOrDefaultAsync(x => x.AgentId == agent.Id && x.Status == DeploymentStatus.Active, cancellationToken)
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

        try
        {
            var toolOutput = await toolExecutor.ExecuteToolsAsync(organizationId, agent, input, cancellationToken);
            var prompt = $"Agent: {agent.Name}\nInput:{JsonSerializer.Serialize(input)}\nTools:{JsonSerializer.Serialize(toolOutput)}";
            var (output, promptTokens, completionTokens, cost) = await modelProvider.GenerateAsync(prompt, cancellationToken);
            var combinedOutput = new Dictionary<string, object?>(toolOutput) { ["modelResponse"] = output };

            run.Status = RunStatus.Completed;
            run.OutputJson = JsonSerializer.Serialize(combinedOutput);
            run.PromptTokens = promptTokens;
            run.CompletionTokens = completionTokens;
            run.EstimatedCostUsd = cost;
            run.CompletedAtUtc = DateTime.UtcNow;
            await memoryService.RememberAsync(run.Id, run.OutputJson, cancellationToken);

            var subscription = await dbContext.OrganizationSubscriptions.FirstOrDefaultAsync(
                x => x.OrganizationId == organizationId && x.IsActive, cancellationToken);
            if (subscription is not null)
            {
                subscription.UsedRunsThisMonth += 1;
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            return new InvokeAgentResponse(run.Id, run.Status.ToString(), combinedOutput, promptTokens, completionTokens, cost);
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

public sealed class AgentModelProvider(IConfiguration configuration) : IAgentModelProvider
{
    public Task<(string output, int promptTokens, int completionTokens, decimal cost)> GenerateAsync(string prompt, CancellationToken cancellationToken)
    {
        var mode = configuration["AI:Mode"] ?? "mock";
        if (string.Equals(mode, "mock", StringComparison.OrdinalIgnoreCase))
        {
            var promptTokens = Math.Max(30, prompt.Length / 4);
            var completionTokens = 120;
            var cost = Math.Round((promptTokens + completionTokens) * 0.00001m, 6);
            return Task.FromResult<(string, int, int, decimal)>(($"mock-response::{prompt[..Math.Min(40, prompt.Length)]}", promptTokens, completionTokens, cost));
        }

        return Task.FromResult<(string, int, int, decimal)>(("provider-not-configured-fallback", 80, 50, 0.0020m));
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
    AgenticFactoryDbContext dbContext) : IAgentRuntime
{
    public async Task TickAsync(CancellationToken cancellationToken)
    {
        var triggers = await dbContext.AgentTriggers
            .Include(x => x.Agent)
            .Where(x => x.IsEnabled && x.Type == TriggerType.Interval)
            .ToListAsync(cancellationToken);

        foreach (var trigger in triggers)
        {
            var shouldRun = !trigger.LastTriggeredAtUtc.HasValue || DateTime.UtcNow - trigger.LastTriggeredAtUtc.Value > TimeSpan.FromMinutes(1);
            if (!shouldRun || trigger.Agent is null)
            {
                continue;
            }

            var deployment = await dbContext.AgentDeployments
                .Where(x => x.AgentId == trigger.AgentId && x.Status == DeploymentStatus.Active)
                .OrderByDescending(x => x.ActivatedAtUtc)
                .FirstOrDefaultAsync(cancellationToken);
            if (deployment is null)
            {
                continue;
            }

            trigger.LastTriggeredAtUtc = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var heartbeat = await dbContext.RuntimeHeartbeats.FirstOrDefaultAsync(x => x.NodeName == Environment.MachineName, cancellationToken);
        if (heartbeat is null)
        {
            heartbeat = new RuntimeHeartbeat
            {
                NodeName = Environment.MachineName,
                Status = "Healthy",
                ActiveTriggerCount = triggers.Count
            };
            dbContext.RuntimeHeartbeats.Add(heartbeat);
        }
        else
        {
            heartbeat.Status = "Healthy";
            heartbeat.LastSeenUtc = DateTime.UtcNow;
            heartbeat.ActiveTriggerCount = triggers.Count;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}

public class RunStatusHub : Hub;

public sealed class IdentitySeedService(
    AgenticFactoryDbContext dbContext,
    UserManager<AppIdentityUser> userManager,
    RoleManager<IdentityRole<Guid>> roleManager)
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
                MaxAgents = 20,
                MaxRunsPerMonth = 5000,
                MonthlyPriceUsd = 99
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
    }
}

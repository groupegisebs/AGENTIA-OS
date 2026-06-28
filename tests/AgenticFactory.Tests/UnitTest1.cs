using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AgenticFactory.Api.Controllers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace AgenticFactory.Tests;

public class ApiIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ApiIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Database:Provider", "inmemory");
            builder.UseEnvironment("Development");
        });
    }

    [Fact]
    public async Task RegisterAndLogin_ReturnsJwt()
    {
        var client = _factory.CreateClient();
        var email = $"creator-{Guid.NewGuid():N}@example.com";
        var password = "Pass123$Test";

        var register = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest("Org Test", email, "Creator", password));
        register.EnsureSuccessStatusCode();

        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));
        login.EnsureSuccessStatusCode();

        var payload = await login.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(payload.TryGetProperty("accessToken", out var token));
        Assert.False(string.IsNullOrWhiteSpace(token.GetString()));
    }

    [Fact]
    public async Task ChatDeployInvoke_WorksInMockMode()
    {
        var client = _factory.CreateClient();
        var email = $"creator-{Guid.NewGuid():N}@example.com";
        var password = "Pass123$Test";

        await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest("Org Flow", email, "Flow User", password));
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));
        var loginJson = await login.Content.ReadFromJsonAsync<JsonElement>();
        var token = loginJson.GetProperty("accessToken").GetString()!;
        var orgId = loginJson.GetProperty("organizationId").GetString()!;

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.Add("X-Organization-Id", orgId);

        var chat = await client.PostAsJsonAsync("/api/agent-creation/chat", new { message = "Create an incident triage agent", existingAgentId = (Guid?)null });
        chat.EnsureSuccessStatusCode();
        var blueprint = await chat.Content.ReadFromJsonAsync<JsonElement>();
        var blueprintId = blueprint.GetProperty("id").GetGuid();
        var agentId = blueprint.GetProperty("agentId").GetGuid();

        var deploy = await client.PostAsJsonAsync("/api/agents/deploy", new { agentId, blueprintId, environment = "dev" });
        deploy.EnsureSuccessStatusCode();
        var deployJson = await deploy.Content.ReadFromJsonAsync<JsonElement>();
        var endpointSlug = deployJson.GetProperty("endpointSlug").GetString()!;
        var apiKey = deployJson.GetProperty("plainApiKey").GetString()!;

        var invokeRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/agents/{endpointSlug}/invoke")
        {
            Content = JsonContent.Create(new { input = new Dictionary<string, object?> { ["ticketId"] = "INC-1001" } })
        };
        invokeRequest.Headers.Add("X-Agent-Key", apiKey);
        var invoke = await client.SendAsync(invokeRequest);
        invoke.EnsureSuccessStatusCode();

        var invokeJson = await invoke.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Completed", invokeJson.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Dashboard_IsScopedToCurrentTenant()
    {
        var client = _factory.CreateClient();

        var tenantA = await RegisterAndLoginAsync(client);
        var tenantAAgent = await CreateAndDeployAgentAsync(client, tenantA.Token, tenantA.OrganizationId);
        await InvokeAgentAsync(client, tenantA.OrganizationId, tenantAAgent.EndpointSlug, tenantAAgent.ApiKey);

        var tenantB = await RegisterAndLoginAsync(client);
        var dashboard = await GetDashboardAsync(client, tenantB.Token, tenantB.OrganizationId);

        Assert.Equal(0, dashboard.GetProperty("stats").GetProperty("totalRuns").GetInt32());
        Assert.Empty(dashboard.GetProperty("recentRuns").EnumerateArray());
    }

    [Fact]
    public async Task Invoke_Fails_WhenOrganizationHeaderDoesNotMatchAgentTenant()
    {
        var client = _factory.CreateClient();

        var tenantA = await RegisterAndLoginAsync(client);
        var tenantAAgent = await CreateAndDeployAgentAsync(client, tenantA.Token, tenantA.OrganizationId);
        var tenantB = await RegisterAndLoginAsync(client);

        var invokeRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/agents/{tenantAAgent.EndpointSlug}/invoke")
        {
            Content = JsonContent.Create(new { input = new Dictionary<string, object?> { ["payload"] = "cross-tenant" } })
        };
        invokeRequest.Headers.Add("X-Agent-Key", tenantAAgent.ApiKey);
        invokeRequest.Headers.Add("X-Organization-Id", tenantB.OrganizationId);

        var response = await client.SendAsync(invokeRequest);

        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task BillingCheckout_ReturnsError_WhenPayGatewayNotConfigured()
    {
        var client = _factory.CreateClient();
        var (token, orgId) = await RegisterAndLoginAsync(client);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.Remove("X-Organization-Id");
        client.DefaultRequestHeaders.Add("X-Organization-Id", orgId);

        var plansResponse = await client.GetAsync("/api/billing/plans");
        plansResponse.EnsureSuccessStatusCode();
        var plans = await plansResponse.Content.ReadFromJsonAsync<JsonElement>();
        var planId = plans.EnumerateArray().First().GetProperty("id").GetGuid();

        var checkout = await client.PostAsJsonAsync("/api/billing/checkout", new
        {
            subscriptionPlanId = planId,
            successUrl = "http://localhost/Subscriptions/Success",
            cancelUrl = "http://localhost/Subscriptions/Cancel"
        });

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, checkout.StatusCode);
        var body = await checkout.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("message", out var message));
        Assert.Contains("pas encore configuré", message.GetString(), StringComparison.OrdinalIgnoreCase);
    }

    private async Task<(string Token, string OrganizationId)> RegisterAndLoginAsync(HttpClient client)
    {
        var email = $"creator-{Guid.NewGuid():N}@example.com";
        const string password = "Pass123$Test";

        var register = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest($"Org-{Guid.NewGuid():N}", email, "Creator", password));
        register.EnsureSuccessStatusCode();

        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));
        login.EnsureSuccessStatusCode();
        var loginJson = await login.Content.ReadFromJsonAsync<JsonElement>();

        return (
            loginJson.GetProperty("accessToken").GetString()!,
            loginJson.GetProperty("organizationId").GetString()!);
    }

    private async Task<(string EndpointSlug, string ApiKey)> CreateAndDeployAgentAsync(HttpClient client, string token, string organizationId)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.Remove("X-Organization-Id");
        client.DefaultRequestHeaders.Add("X-Organization-Id", organizationId);

        var chat = await client.PostAsJsonAsync("/api/agent-creation/chat", new { message = "Create an incident triage agent", existingAgentId = (Guid?)null });
        chat.EnsureSuccessStatusCode();
        var blueprint = await chat.Content.ReadFromJsonAsync<JsonElement>();
        var blueprintId = blueprint.GetProperty("id").GetGuid();
        var agentId = blueprint.GetProperty("agentId").GetGuid();

        var deploy = await client.PostAsJsonAsync("/api/agents/deploy", new { agentId, blueprintId, environment = "dev" });
        deploy.EnsureSuccessStatusCode();
        var deployJson = await deploy.Content.ReadFromJsonAsync<JsonElement>();

        return (
            deployJson.GetProperty("endpointSlug").GetString()!,
            deployJson.GetProperty("plainApiKey").GetString()!);
    }

    private async Task InvokeAgentAsync(HttpClient client, string organizationId, string endpointSlug, string apiKey)
    {
        var invokeRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/agents/{endpointSlug}/invoke")
        {
            Content = JsonContent.Create(new { input = new Dictionary<string, object?> { ["ticketId"] = "INC-1001" } })
        };
        invokeRequest.Headers.Add("X-Agent-Key", apiKey);
        invokeRequest.Headers.Add("X-Organization-Id", organizationId);

        var invoke = await client.SendAsync(invokeRequest);
        invoke.EnsureSuccessStatusCode();
    }

    private async Task<JsonElement> GetDashboardAsync(HttpClient client, string token, string organizationId)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/monitoring/dashboard");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add("X-Organization-Id", organizationId);
        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>())!;
    }
}

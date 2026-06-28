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
}

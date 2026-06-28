using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AgentiaOs.Application.Contracts.Auth;
using AgentiaOs.Application.Contracts.Conversations;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace AgentiaOs.Api.Tests;

public sealed class ApiIntegrationTests : IClassFixture<ApiIntegrationTests.CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public ApiIntegrationTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Health_ReturnsOk()
    {
        var response = await _client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Register_ThenLogin_ReturnsJwt()
    {
        var email = $"user-{Guid.NewGuid():N}@agentia.local";
        var register = new RegisterRequest(email, "Passw0rd!123", "Alice");

        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register", register);
        var auth = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>();

        Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);
        Assert.NotNull(auth);
        Assert.False(string.IsNullOrWhiteSpace(auth!.Token));

        var login = new LoginRequest(email, "Passw0rd!123");
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", login);
        var loginAuth = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        Assert.NotNull(loginAuth);
        Assert.False(string.IsNullOrWhiteSpace(loginAuth!.Token));
    }

    [Fact]
    public async Task Conversation_HappyPath_Works()
    {
        var email = $"conv-{Guid.NewGuid():N}@agentia.local";
        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(email, "Passw0rd!123", "Bob"));
        var auth = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.Token);

        var createConversationResponse = await _client.PostAsJsonAsync("/api/conversations", new CreateConversationRequest("Demo conversation"));
        createConversationResponse.EnsureSuccessStatusCode();
        var createdConversation = await createConversationResponse.Content.ReadFromJsonAsync<ConversationDto>();
        Assert.NotNull(createdConversation);

        var postMessageResponse = await _client.PostAsJsonAsync(
            $"/api/conversations/{createdConversation!.Id}/messages",
            new PostMessageRequest("Bonjour"));
        postMessageResponse.EnsureSuccessStatusCode();

        var getConversationResponse = await _client.GetAsync($"/api/conversations/{createdConversation.Id}");
        getConversationResponse.EnsureSuccessStatusCode();
    }

    public sealed class CustomWebApplicationFactory : WebApplicationFactory<Program>
    {
        private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"agentiaos-tests-{Guid.NewGuid():N}.db");

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");

            builder.ConfigureAppConfiguration((_, configBuilder) =>
            {
                var settings = new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Default"] = $"Data Source={_dbPath}",
                    ["Jwt:Issuer"] = "AgentiaOs",
                    ["Jwt:Audience"] = "AgentiaOs.Client",
                    ["Jwt:SigningKey"] = "Phase1DevOnlyChangeMe_WithAtLeast32Chars!"
                };

                configBuilder.AddInMemoryCollection(settings);
            });
        }
    }

    private sealed record ConversationDto(Guid Id, string Title);
}

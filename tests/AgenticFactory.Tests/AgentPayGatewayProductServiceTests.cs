using AgenticFactory.Application;
using AgenticFactory.Domain;
using AgenticFactory.Infrastructure.Billing;
using AgenticFactory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AgenticFactory.Tests;

public class AgentPayGatewayProductServiceTests
{
    [Fact]
    public async Task TryEnsureAgentProduct_ReturnsNull_WhenPayGatewayNotConfigured()
    {
        await using var db = CreateDbContext();
        var orgId = Guid.NewGuid();
        var agent = CreateAgent(orgId);
        var client = new FakePayGatewayClient { IsConfiguredValue = false };
        var service = CreateService(db, client);

        var result = await service.TryEnsureAgentProductAsync(agent, orgId, CancellationToken.None);

        Assert.Null(result);
        Assert.Null(agent.PayGatewayProductCode);
        Assert.Empty(client.Requests);
    }

    [Fact]
    public async Task TryEnsureAgentProduct_ReturnsProductCode_WhenCatalogCreated()
    {
        await using var db = CreateDbContext();
        var orgId = Guid.NewGuid();
        var agent = CreateAgent(orgId);
        var expectedCode = new GisebsApiPayGatewayOptions().BuildAgentProductCode(agent.Id);
        var client = new FakePayGatewayClient
        {
            IsConfiguredValue = true,
            NextResult = new GisebsCatalogItemResult(GisebsCatalogItemOutcome.Created, expectedCode)
        };
        var service = CreateService(db, client);

        var result = await service.TryEnsureAgentProductAsync(agent, orgId, CancellationToken.None);

        Assert.Equal(expectedCode, result);
        Assert.Single(client.Requests);
        Assert.Equal(expectedCode, client.Requests[0].ProductCode);
        Assert.Equal("ONE-TIME", client.Requests[0].PlanCode);
        Assert.Equal(agent.Name, client.Requests[0].ProductName);
    }

    [Fact]
    public async Task TryEnsureAgentProduct_IsIdempotent_WhenProductAlreadyExists()
    {
        await using var db = CreateDbContext();
        var orgId = Guid.NewGuid();
        var agent = CreateAgent(orgId);
        var expectedCode = new GisebsApiPayGatewayOptions().BuildAgentProductCode(agent.Id);
        var client = new FakePayGatewayClient
        {
            IsConfiguredValue = true,
            NextResult = new GisebsCatalogItemResult(GisebsCatalogItemOutcome.AlreadyExists, expectedCode)
        };
        var service = CreateService(db, client);

        var result = await service.TryEnsureAgentProductAsync(agent, orgId, CancellationToken.None);

        Assert.Equal(expectedCode, result);
    }

    [Fact]
    public async Task TryEnsureAgentProduct_SkipsGateway_WhenProductCodeAlreadySet()
    {
        await using var db = CreateDbContext();
        var orgId = Guid.NewGuid();
        var agent = CreateAgent(orgId);
        agent.PayGatewayProductCode = "AGENTIA-AGENT-EXISTING";
        var client = new FakePayGatewayClient { IsConfiguredValue = true };
        var service = CreateService(db, client);

        var result = await service.TryEnsureAgentProductAsync(agent, orgId, CancellationToken.None);

        Assert.Equal("AGENTIA-AGENT-EXISTING", result);
        Assert.Empty(client.Requests);
    }

    [Fact]
    public async Task TryEnsureAgentProduct_ReturnsNull_WhenGatewayFails()
    {
        await using var db = CreateDbContext();
        var orgId = Guid.NewGuid();
        var agent = CreateAgent(orgId);
        var client = new FakePayGatewayClient
        {
            IsConfiguredValue = true,
            NextResult = new GisebsCatalogItemResult(
                GisebsCatalogItemOutcome.Failed,
                new GisebsApiPayGatewayOptions().BuildAgentProductCode(agent.Id),
                "erreur réseau")
        };
        var service = CreateService(db, client);

        var result = await service.TryEnsureAgentProductAsync(agent, orgId, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public void BuildAgentProductCode_UsesShortAgentId()
    {
        var options = new GisebsApiPayGatewayOptions();
        var agentId = Guid.Parse("a1b2c3d4-e5f6-7890-abcd-ef1234567890");

        var code = options.BuildAgentProductCode(agentId);

        Assert.Equal("AGENTIA-AGENT-A1B2C3D4", code);
    }

    private static AgentPayGatewayProductService CreateService(
        AgenticFactoryDbContext db,
        FakePayGatewayClient client)
    {
        return new AgentPayGatewayProductService(
            db,
            client,
            Options.Create(new GisebsApiPayGatewayOptions()),
            NullLogger<AgentPayGatewayProductService>.Instance);
    }

    private static AgenticFactoryDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AgenticFactoryDbContext>()
            .UseInMemoryDatabase($"agent-paygateway-{Guid.NewGuid():N}")
            .Options;
        return new AgenticFactoryDbContext(options);
    }

    private static Agent CreateAgent(Guid organizationId) =>
        new()
        {
            OrganizationId = organizationId,
            Name = "Agent de test",
            Description = "Description test",
            EndpointSlug = $"agent-{Guid.NewGuid():N}"[..18],
            Status = AgentStatus.Draft
        };

    private sealed class FakePayGatewayClient : IGisebsPayGatewayClient
    {
        public bool IsConfiguredValue { get; set; }
        public GisebsCatalogItemResult NextResult { get; set; } =
            new(GisebsCatalogItemOutcome.Failed, Detail: "non configuré");

        public List<GisebsCatalogItemRequest> Requests { get; } = [];

        public bool IsConfigured => IsConfiguredValue;

        public Task<BillingCheckoutResult> CreateCheckoutSessionAsync(
            GisebsCheckoutSessionRequest request,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<GisebsPaymentStatus?> GetPaymentStatusAsync(
            string paymentCode,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<GisebsCatalogItemResult> TryCreateCatalogItemAsync(
            GisebsCatalogItemRequest request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(NextResult with { ProductCode = NextResult.ProductCode ?? request.ProductCode });
        }
    }
}

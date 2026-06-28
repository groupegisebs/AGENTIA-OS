using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AgenticFactory.Shared;

namespace AgenticFactory.Web.Services;

public record LoginRequest(string Email, string Password);
public record RegisterRequest(string Email, string Password, string FullName, string OrganizationName);
public record AuthResponse(
    [property: JsonPropertyName("accessToken")] string Token,
    string Email,
    string FullName,
    string OrganizationId,
    string Role);
public record RunItemResponse(
    Guid Id,
    int Status,
    DateTime CreatedAtUtc,
    DateTime? StartedAtUtc,
    DateTime? CompletedAtUtc,
    decimal EstimatedCostUsd,
    int PromptTokens,
    int CompletionTokens,
    string? AgentName);

public record DailyRunChartResponse(string Label, int Success, int Failed, int Running, int Queued);
public record StatusBreakdownResponse(string Status, int Count);

public record DashboardResponse(
    DashboardStatsDto Stats,
    int ActiveAgents,
    List<RunItemResponse>? RecentRuns,
    [property: JsonPropertyName("runtime")] List<RuntimeStatusDto>? RuntimeStatuses,
    List<DailyRunChartResponse>? DailyRuns,
    List<StatusBreakdownResponse>? StatusBreakdown,
    List<int>? TokenSeries);

public record DeployRequest(Guid AgentId, Guid BlueprintId, string Environment);
public record DeployResponse(
    Guid AgentId,
    Guid AgentVersionId,
    Guid DeploymentId,
    string EndpointSlug,
    string PlainApiKey);
public record ChatRequest(string Message, Guid? ExistingAgentId = null);
public record BlueprintResult(
    Guid Id,
    Guid AgentId,
    string PromptSummary,
    string BlueprintJson,
    string Status,
    string ValidationNotes);
public record AgentsSummaryResponse(int Total, int Active, int Running, int Paused, int Disabled);
public record AgentListItemResponse(
    Guid Id,
    string Name,
    string Description,
    string EndpointSlug,
    string Status,
    string DisplayStatus,
    DateTime CreatedAtUtc,
    Guid? LatestBlueprintId,
    int? VersionNumber,
    string Environment,
    DateTime? LastRunAt,
    int RunsLast7Days,
    int RunsLast30Days,
    decimal CostLast30Days,
    int[] RunsSparkline);
public record AgentsPageResponse(AgentsSummaryResponse Summary, List<AgentListItemResponse> Agents);
public record DeploymentListItemResponse(
    Guid Id,
    Guid AgentId,
    string AgentName,
    string EndpointSlug,
    int VersionNumber,
    string Status,
    string Environment,
    DateTime? ActivatedAtUtc,
    DateTime CreatedAtUtc);
public record RunListItemResponse(
    Guid Id,
    Guid AgentId,
    string AgentName,
    int Status,
    DateTime CreatedAtUtc,
    decimal EstimatedCostUsd,
    int PromptTokens,
    int CompletionTokens,
    string ErrorMessage);

public record DeploymentDetailResponse(
    DeploymentAgentInfo Agent,
    DeploymentVersionInfo? CurrentVersion,
    List<PipelineStageResponse> Pipeline,
    List<VersionRowResponse> Versions,
    ProductionDetailResponse? Production,
    UsageDetailResponse Usage,
    List<TimelineResponse> RecentTimeline,
    List<OperationLogResponse> OperationLogs,
    Guid? LatestBlueprintId);

public record DeploymentAgentInfo(Guid Id, string Name, string Description, string EndpointSlug, string Status, string InvokeUrl);
public record DeploymentVersionInfo(Guid Id, int VersionNumber, string Label);
public record PipelineStageResponse(string Stage, string Label, string Status, DateTime? DeployedAt, int? VersionNumber, string? VersionLabel);
public record VersionRowResponse(Guid Id, int VersionNumber, string Label, string Description, DateTime CreatedAtUtc, string CreatedBy, bool IsCurrent, string Status);
public record ProductionDetailResponse(string Environment, string Status, DateTime? ActivatedAtUtc, string ApiKeyMasked, string RuntimeNode, string RuntimeStatus, int UptimeHours, int UptimeDays, List<TriggerResponse> Triggers);
public record TriggerResponse(string Type, bool IsEnabled);
public record UsageDetailResponse(int Runs, long Tokens, decimal Cost, int Errors, List<int> TokenSeries);
public record TimelineResponse(string Environment, string VersionLabel, DateTime At, string Outcome);
public record OperationLogResponse(DateTime At, string Level, string Message);

public class ApiClient(HttpClient http)
{
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public void SetBearerToken(string token) =>
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    public async Task<AuthResponse?> LoginAsync(string email, string password)
    {
        var body = JsonSerializer.Serialize(new LoginRequest(email, password), _json);
        var response = await http.PostAsync("/api/auth/login",
            new StringContent(body, Encoding.UTF8, "application/json"));
        if (!response.IsSuccessStatusCode) return null;
        var content = await response.Content.ReadAsStringAsync();
        var auth = JsonSerializer.Deserialize<AuthResponse>(content, _json);
        return string.IsNullOrWhiteSpace(auth?.Token) ? null : auth;
    }

    public async Task<(AuthResponse? Result, string? Error)> RegisterAsync(string email, string password, string fullName, string orgName)
    {
        var body = JsonSerializer.Serialize(new RegisterRequest(email, password, fullName, orgName), _json);
        var response = await http.PostAsync("/api/auth/register",
            new StringContent(body, Encoding.UTF8, "application/json"));
        var content = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            try
            {
                var err = JsonSerializer.Deserialize<JsonElement>(content, _json);
                return (null, err.TryGetProperty("message", out var m) ? m.GetString() : content);
            }
            catch { return (null, content); }
        }
        return (JsonSerializer.Deserialize<AuthResponse>(content, _json), null);
    }

    public async Task<DashboardResponse?> GetDashboardAsync()
    {
        var response = await http.GetAsync("/api/monitoring/dashboard");
        if (!response.IsSuccessStatusCode) return null;
        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<DashboardResponse>(content, _json);
    }

    public async Task<AgentsPageResponse?> GetAgentsAsync()
    {
        var response = await http.GetAsync("/api/agents");
        if (!response.IsSuccessStatusCode) return null;
        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<AgentsPageResponse>(content, _json);
    }

    public async Task<List<DeploymentListItemResponse>?> GetDeploymentsAsync()
    {
        var response = await http.GetAsync("/api/agents/deployments");
        if (!response.IsSuccessStatusCode) return null;
        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<List<DeploymentListItemResponse>>(content, _json);
    }

    public async Task<DeploymentDetailResponse?> GetDeploymentDetailAsync(Guid agentId)
    {
        var response = await http.GetAsync($"/api/agents/{agentId}/deployments/detail");
        if (!response.IsSuccessStatusCode) return null;
        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<DeploymentDetailResponse>(content, _json);
    }

    public async Task<List<RunListItemResponse>?> GetRunsAsync(int limit = 50)
    {
        var response = await http.GetAsync($"/api/monitoring/runs?limit={limit}");
        if (!response.IsSuccessStatusCode) return null;
        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<List<RunListItemResponse>>(content, _json);
    }

    public async Task<BlueprintResult?> CreateAgentFromChatAsync(string message)
    {
        var body = JsonSerializer.Serialize(new ChatRequest(message), _json);
        var response = await http.PostAsync("/api/agent-creation/chat",
            new StringContent(body, Encoding.UTF8, "application/json"));
        if (!response.IsSuccessStatusCode) return null;
        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<BlueprintResult>(content, _json);
    }

    public async Task<(DeployResponse? Result, string? Error)> DeployAgentAsync(
        Guid agentId, Guid blueprintId, string environment = "production")
    {
        var body = JsonSerializer.Serialize(new DeployRequest(agentId, blueprintId, environment), _json);
        var response = await http.PostAsync("/api/agents/deploy",
            new StringContent(body, Encoding.UTF8, "application/json"));
        var content = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
            return (null, content);
        return (JsonSerializer.Deserialize<DeployResponse>(content, _json), null);
    }
}

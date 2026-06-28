using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace AgenticFactory.Web.Services;

public record LoginRequest(string Email, string Password);
public record RegisterRequest(string Email, string Password, string FullName, string OrganizationName);
public record AuthResponse(string Token, string Email, string FullName, string OrganizationId, string Role);
public record DashboardStatsResponse(
    int TotalAgents, int TotalRuns, int TotalErrors,
    long TotalTokens, double TotalCostUsd,
    int TodayRuns, int TodayErrors, long TodayTokens, double TodayCostUsd);
public record RunItemResponse(Guid Id, string Status, DateTime CreatedAt, double CostUsd, int PromptTokens, int CompletionTokens);
public record RuntimeStatusResponse(string NodeName, string Status, DateTime LastSeen, int ActiveTriggers);
public record DashboardResponse(DashboardStatsResponse Stats, List<RunItemResponse> RecentRuns, List<RuntimeStatusResponse> RuntimeStatuses);

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
        return JsonSerializer.Deserialize<AuthResponse>(content, _json);
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
}

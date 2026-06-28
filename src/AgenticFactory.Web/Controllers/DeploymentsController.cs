using System.Security.Claims;
using System.Text.Json;
using AgenticFactory.Web.Models;
using AgenticFactory.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace AgenticFactory.Web.Controllers;

public class DeploymentsController(ApiClient api) : AuthenticatedController
{
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public async Task<IActionResult> Index()
    {
        SetActiveNav("Deployments");
        ViewData["DeploymentsMode"] = true;
        AuthenticateApi(api);

        var items = await api.GetDeploymentsAsync();
        var groups = (items ?? [])
            .GroupBy(d => d.AgentId)
            .Select(g =>
            {
                var latest = g.OrderByDescending(x => x.ActivatedAtUtc ?? x.CreatedAtUtc).First();
                return new DeploymentAgentGroup(
                    g.Key,
                    latest.AgentName,
                    latest.EndpointSlug,
                    $"v1.{latest.VersionNumber}.0",
                    latest.Environment,
                    latest.Status,
                    latest.ActivatedAtUtc ?? latest.CreatedAtUtc,
                    g.Count());
            })
            .OrderByDescending(g => g.LastDeployedAt)
            .ToList();

        return View(new DeploymentsIndexViewModel { AgentGroups = groups });
    }

    public async Task<IActionResult> Detail(Guid id)
    {
        SetActiveNav("Deployments");
        ViewData["DeploymentsMode"] = true;
        AuthenticateApi(api);

        var detail = await api.GetDeploymentDetailAsync(id);
        if (detail is null)
            return RedirectToAction(nameof(Index));

        return View(BuildDetailViewModel(detail));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TestInvoke(Guid id, TestInvokeFormModel testInvoke)
    {
        SetActiveNav("Deployments");
        ViewData["DeploymentsMode"] = true;
        AuthenticateApi(api);

        var detail = await api.GetDeploymentDetailAsync(id);
        if (detail is null)
            return RedirectToAction(nameof(Index));

        var vm = BuildDetailViewModel(detail);
        vm.TestInvoke = new TestInvokeFormModel
        {
            InputJson = string.IsNullOrWhiteSpace(testInvoke.InputJson)
                ? """{"message": "Test invoke"}"""
                : testInvoke.InputJson
        };

        if (string.IsNullOrWhiteSpace(testInvoke.ApiKey))
        {
            vm.InvokeResult = new InvokeTestResultViewModel
            {
                Success = false,
                ErrorMessage = "La clé API est requise. Collez la clé affichée une seule fois lors du déploiement."
            };
            return View("Detail", vm);
        }

        if (!TryParseInputJson(vm.TestInvoke.InputJson, out var input, out var parseError))
        {
            vm.InvokeResult = new InvokeTestResultViewModel
            {
                Success = false,
                ErrorMessage = parseError
            };
            return View("Detail", vm);
        }

        var organizationId = User.FindFirstValue("OrganizationId") ?? "";
        var (result, error, statusCode) = await api.InvokeAgentAsync(
            detail.Agent.EndpointSlug,
            testInvoke.ApiKey.Trim(),
            organizationId,
            input);

        if (result is null)
        {
            vm.InvokeResult = new InvokeTestResultViewModel
            {
                Success = false,
                HttpStatus = statusCode,
                ErrorMessage = TranslateInvokeError(statusCode, error)
            };
            return View("Detail", vm);
        }

        vm.InvokeResult = new InvokeTestResultViewModel
        {
            Success = true,
            HttpStatus = statusCode,
            RunId = result.RunId,
            Status = result.Status,
            OutputJson = result.Output is null or { Count: 0 }
                ? null
                : JsonSerializer.Serialize(result.Output, _jsonOptions),
            PromptTokens = result.PromptTokens,
            CompletionTokens = result.CompletionTokens,
            EstimatedCostUsd = result.EstimatedCostUsd
        };

        return View("Detail", vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Redeploy(Guid agentId, Guid blueprintId, string environment = "production")
    {
        AuthenticateApi(api);
        var (result, error) = await api.DeployAgentAsync(agentId, blueprintId, environment);
        if (result is null)
            TempData["Error"] = string.IsNullOrWhiteSpace(error) ? "Échec du redéploiement." : error;
        else
        {
            TempData["Success"] = $"Redéploiement réussi — {result.EndpointSlug}";
            TempData["ApiKey"] = result.PlainApiKey;
        }
        return RedirectToAction(nameof(Detail), new { id = agentId });
    }

    private static DeploymentDetailViewModel BuildDetailViewModel(DeploymentDetailResponse detail) =>
        new()
        {
            AgentId = detail.Agent.Id,
            AgentName = detail.Agent.Name,
            Description = detail.Agent.Description,
            EndpointSlug = detail.Agent.EndpointSlug,
            InvokeUrl = detail.Agent.InvokeUrl,
            VersionLabel = detail.CurrentVersion?.Label ?? "—",
            AgentStatus = detail.Agent.Status,
            LatestBlueprintId = detail.LatestBlueprintId,
            Pipeline = detail.Pipeline.Select(p => new PipelineStageItem(
                p.Stage, p.Label, p.Status, p.DeployedAt, p.VersionLabel)).ToList(),
            Versions = detail.Versions.Select(v => new VersionRowItem(
                v.Id, v.Label, v.Description, v.CreatedAtUtc, v.CreatedBy, v.IsCurrent, TranslateVersionStatus(v.Status))).ToList(),
            Production = detail.Production is null ? null : new ProductionDetailItem(
                detail.Production.Environment,
                detail.Production.Status,
                detail.Production.ActivatedAtUtc,
                detail.Production.ApiKeyMasked,
                detail.Production.RuntimeNode,
                detail.Production.RuntimeStatus,
                detail.Production.UptimeDays,
                detail.Production.UptimeHours,
                detail.Production.Triggers.Where(t => t.IsEnabled).Select(t => t.Type).ToList()),
            Usage = new UsageDetailItem(
                detail.Usage.Runs,
                detail.Usage.Tokens,
                detail.Usage.Cost,
                detail.Usage.Errors,
                detail.Usage.TokenSeries),
            RecentTimeline = detail.RecentTimeline.Select(t => new TimelineItem(
                Capitalize(t.Environment), t.VersionLabel, t.At, t.Outcome)).ToList(),
            OperationLogs = detail.OperationLogs.Select(l => new OperationLogItem(l.At, l.Level, l.Message)).ToList()
        };

    private static bool TryParseInputJson(string json, out Dictionary<string, object?> input, out string? error)
    {
        input = new Dictionary<string, object?>();
        error = null;

        if (string.IsNullOrWhiteSpace(json))
            return true;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                error = "L'entrée JSON doit être un objet (ex. {\"message\": \"Test invoke\"}).";
                return false;
            }

            input = JsonElementToDictionary(doc.RootElement);
            return true;
        }
        catch (JsonException)
        {
            error = "JSON d'entrée invalide.";
            return false;
        }
    }

    private static Dictionary<string, object?> JsonElementToDictionary(JsonElement element)
    {
        var dict = new Dictionary<string, object?>();
        foreach (var prop in element.EnumerateObject())
            dict[prop.Name] = JsonElementToObject(prop.Value);
        return dict;
    }

    private static object? JsonElementToObject(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Object => JsonElementToDictionary(element),
        JsonValueKind.Array => element.EnumerateArray().Select(JsonElementToObject).ToList(),
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Number => element.TryGetInt64(out var l) ? l : element.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null => null,
        _ => element.GetRawText()
    };

    private static string TranslateInvokeError(int? statusCode, string? error)
    {
        if (statusCode == 401)
            return "Non autorisé — vérifiez la clé API (X-Agent-Key).";
        if (statusCode == 404)
            return "Agent introuvable ou non déployé.";
        if (statusCode is >= 500)
            return string.IsNullOrWhiteSpace(error) ? "Erreur serveur lors de l'invoke." : error;
        return string.IsNullOrWhiteSpace(error) ? "Échec de l'invoke." : error;
    }

    private static string TranslateVersionStatus(string status) => status switch
    {
        "Deployed" => "Déployée",
        "Ready" => "Prête",
        _ => status
    };

    private static string Capitalize(string s) =>
        string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0]) + s[1..].ToLowerInvariant();
}

using AgenticFactory.Web.Models;
using AgenticFactory.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace AgenticFactory.Web.Controllers;

public class DeploymentsController(ApiClient api) : AuthenticatedController
{
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

        var vm = new DeploymentDetailViewModel
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

        return View(vm);
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

    private static string TranslateVersionStatus(string status) => status switch
    {
        "Deployed" => "Déployée",
        "Ready" => "Prête",
        _ => status
    };

    private static string Capitalize(string s) =>
        string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0]) + s[1..].ToLowerInvariant();
}

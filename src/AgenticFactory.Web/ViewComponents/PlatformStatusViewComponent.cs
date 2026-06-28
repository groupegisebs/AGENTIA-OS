using System.Security.Claims;
using AgenticFactory.Web.Models;
using AgenticFactory.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace AgenticFactory.Web.ViewComponents;

public sealed class PlatformStatusViewComponent(ApiClient api, IHttpContextAccessor httpContextAccessor) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        var token = httpContextAccessor.HttpContext?.User.FindFirstValue("ApiToken");
        if (string.IsNullOrEmpty(token))
            return View(PlatformStatusViewModel.Offline);

        api.SetBearerToken(token);
        var status = await api.GetPlatformStatusAsync();
        if (status is null)
            return View(PlatformStatusViewModel.Offline);

        return View(new PlatformStatusViewModel
        {
            Status = status.Status,
            Label = TranslateStatus(status.Status),
            NodeCount = status.NodeCount
        });
    }

    private static string TranslateStatus(string status) => status switch
    {
        "Healthy" => "En ligne",
        "Degraded" => "Dégradé",
        _ => "Hors ligne"
    };
}

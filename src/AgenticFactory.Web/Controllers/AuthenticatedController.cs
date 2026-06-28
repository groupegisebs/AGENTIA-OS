using System.Security.Claims;
using AgenticFactory.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgenticFactory.Web.Controllers;

[Authorize]
public abstract class AuthenticatedController : Controller
{
    protected void AuthenticateApi(ApiClient api)
    {
        var token = User.FindFirstValue("ApiToken");
        if (!string.IsNullOrEmpty(token))
            api.SetBearerToken(token);
    }

    protected void SetActiveNav(string nav) => ViewData["ActiveNav"] = nav;
}

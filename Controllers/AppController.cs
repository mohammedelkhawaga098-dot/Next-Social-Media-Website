using Microsoft.AspNetCore.Mvc;
using NEXT.Helpers;

namespace NEXT.Controllers;

public abstract class AppController : Controller
{
    protected string UserId => User.GetUserId();

    protected IActionResult Fail(ServiceResult result) =>
        result.IsForbidden ? StatusCode(403, new { error = result.Error }) : BadRequest(new { error = result.Error });

    protected IActionResult Fail<T>(ServiceResult<T> result) =>
        result.IsForbidden ? StatusCode(403, new { error = result.Error }) : BadRequest(new { error = result.Error });

    protected void Toast(string message, bool isError = false)
    {
        TempData["Toast"] = message;
        TempData["ToastType"] = isError ? "error" : "ok";
    }

    protected IActionResult RedirectBack(string? returnUrl, string action, string controller) =>
        !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? LocalRedirect(returnUrl)
            : RedirectToAction(action, controller);
}

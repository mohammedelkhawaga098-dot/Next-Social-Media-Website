using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NEXT.ViewModels;

namespace NEXT.Controllers;

public class HomeController : AppController
{
    public IActionResult Index(FeedScope scope = FeedScope.Network)
    {
        ViewBag.Scope = scope;
        return View();
    }

    [AllowAnonymous, ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() => View(model: HttpContext.TraceIdentifier);

    [AllowAnonymous]
    public IActionResult Status(int id)
    {
        Response.StatusCode = id;
        return View(model: id);
    }
}

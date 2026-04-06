using Microsoft.AspNetCore.Mvc;
using DraftView.Domain.Interfaces.Repositories;

namespace DraftView.Web.Controllers;

public class HomeController(IUserRepository userRepo) : BaseController(userRepo)
{
    public IActionResult Index()
    {
        if (User.Identity?.IsAuthenticated != true)
            return RedirectToAction("Login", "Account");

        if (User.IsInRole("SystemSupport"))
            return RedirectToAction("Dashboard", "Support");

        if (User.IsInRole("Author"))
            return RedirectToAction("Dashboard", "Author");

        return RedirectToAction("Dashboard", "Reader");
    }
}
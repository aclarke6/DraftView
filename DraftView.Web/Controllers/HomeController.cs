using Microsoft.AspNetCore.Mvc;
using DraftView.Domain.Interfaces.Repositories;

namespace DraftView.Web.Controllers;

public class HomeController(IUserRepository userRepo) : BaseController(userRepo)
{
    public async Task<IActionResult> Index()
    {
        if (User.Identity?.IsAuthenticated != true)
            return RedirectToAction("Login", "Account");

        return await IsAuthorAsync()
            ? RedirectToAction("Dashboard", "Author")
            : RedirectToAction("Dashboard", "Reader");
    }
}

using DraftView.Domain.Enumerations;
using DraftView.Domain.Interfaces.Services;
using DraftView.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DraftView.Web.Controllers;

[Authorize(Roles = "SystemSupport")]
public class SupportController(ISystemStateMessageService systemStateMessageService) : Controller
{
    public IActionResult Index() => RedirectToAction("Dashboard");

    public async Task<IActionResult> Dashboard()
    {
        var active  = await systemStateMessageService.GetActiveMessageAsync();
        var all     = await systemStateMessageService.GetAllMessagesAsync();
        var history = all
            .Where(m => !m.IsActive)
            .OrderByDescending(m => m.CreatedAt)
            .ToList();

        var model = new SupportDashboardViewModel
        {
            SystemStatus   = "Operational",
            ActiveAuthors  = 0,
            ActiveReaders  = 0,
            ActiveMessage  = active,
            MessageHistory = history
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PostMessage(
        string message,
        SystemStateMessageSeverity severity,
        CancellationToken ct = default)
    {
        await systemStateMessageService.CreateMessageAsync(message, severity, ct);
        TempData["Success"] = "System state message posted.";
        return RedirectToAction("Dashboard");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RevokeMessage(Guid messageId, CancellationToken ct = default)
    {
        await systemStateMessageService.DeactivateMessageAsync(messageId, ct);
        TempData["Success"] = "System state message revoked.";
        return RedirectToAction("Dashboard");
    }
}

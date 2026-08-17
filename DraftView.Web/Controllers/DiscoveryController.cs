using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;
using DraftView.Domain.Exceptions;
using DraftView.Domain.Interfaces.Repositories;
using DraftView.Domain.Interfaces.Services;
using DraftView.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DraftView.Web.Controllers;

public class DiscoveryController(
    IProjectRepository projectRepo,
    IAccessRequestRepository accessRequestRepo,
    IAccessRequestService accessRequestService,
    IUserRepository userRepo) : BaseController(userRepo)
{
    [AllowAnonymous]
    public async Task<IActionResult> Index()
    {
        var allProjects = await projectRepo.GetAllAsync();
        var openProjects = allProjects.Where(p => p.IsOpen && !p.IsSoftDeleted).ToList();

        var isAuthenticated = User.Identity?.IsAuthenticated == true;
        var rows = new List<DiscoveryProjectViewModel>();

        IReadOnlyList<AccessRequest> readerRequests = [];
        if (isAuthenticated)
        {
            var user = await GetCurrentUserAsync();
            if (user is not null)
            {
                readerRequests = await accessRequestRepo.GetVisibleByReaderIdAsync(
                    user.Id, DateTime.UtcNow.Date);
            }
        }

        foreach (var project in openProjects)
        {
            var status = DiscoveryRequestStatus.None;
            var req = readerRequests.FirstOrDefault(r => r.ProjectId == project.Id);
            if (req is not null)
            {
                status = req.Status switch
                {
                    AccessRequestStatus.Pending  => DiscoveryRequestStatus.Pending,
                    AccessRequestStatus.Approved => DiscoveryRequestStatus.Approved,
                    AccessRequestStatus.Declined => DiscoveryRequestStatus.Declined,
                    _ => DiscoveryRequestStatus.None
                };
            }
            rows.Add(new DiscoveryProjectViewModel { Project = project, RequestStatus = status });
        }

        return View(new DiscoveryViewModel { Projects = rows, IsAuthenticated = isAuthenticated });
    }

    [Authorize(Policy = "RequireBetaReaderPolicy")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitRequest(Guid projectId, string? coverNote, string? contactEmail)
    {
        var user = await GetCurrentUserAsync();
        if (user is null) return Forbid();

        try
        {
            await accessRequestService.SubmitRequestAsync(user.Id, projectId, coverNote, contactEmail);
            TempData["Success"] = "Your request has been sent. You'll be notified if the author accepts.";
        }
        catch (InvariantViolationException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction("Index");
    }
}

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using DraftView.Application.Services;
using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;
using DraftView.Domain.Interfaces.Repositories;
using DraftView.Domain.Interfaces.Services;

namespace DraftView.Application.Tests.Services;

/// <summary>
/// Tests for SyncOrchestrationService.StartSyncAsync.
/// Covers: project not found (no-op), project found (MarkSyncing called and
/// SaveChangesAsync called), and verifying the method does not throw on missing projects.
/// Excludes: background Task.Run execution (fire-and-forget, not awaitable in unit tests),
/// EF Core persistence, ISyncService parse behaviour.
/// </summary>
public class SyncOrchestrationServiceTests
{
    private readonly Mock<IProjectRepository>   _projectRepo  = new();
    private readonly Mock<IUnitOfWork>          _unitOfWork   = new();
    private readonly Mock<IServiceScopeFactory> _scopeFactory = new();

    private SyncOrchestrationService CreateSut() => new(
        _projectRepo.Object,
        _unitOfWork.Object,
        _scopeFactory.Object,
        NullLogger<SyncOrchestrationService>.Instance);

    [Fact]
    public async Task StartSyncAsync_ProjectNotFound_DoesNotSaveChanges()
    {
        var projectId = Guid.NewGuid();
        _projectRepo.Setup(r => r.GetByIdAsync(projectId, default)).ReturnsAsync((Project?)null);

        await CreateSut().StartSyncAsync(projectId);

        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task StartSyncAsync_ProjectFound_MarksProjectAsSyncingAndSaves()
    {
        var authorId  = Guid.NewGuid();
        var project   = Project.Create("Book", "/path", authorId, "sync-root");
        var projectId = project.Id;

        _projectRepo.Setup(r => r.GetByIdAsync(projectId, default)).ReturnsAsync(project);

        await CreateSut().StartSyncAsync(projectId);

        Assert.Equal(SyncStatus.Syncing, project.SyncStatus);
        _unitOfWork.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task StartSyncAsync_ProjectNotFound_DoesNotThrow()
    {
        var projectId = Guid.NewGuid();
        _projectRepo.Setup(r => r.GetByIdAsync(projectId, default)).ReturnsAsync((Project?)null);

        var exception = await Record.ExceptionAsync(() => CreateSut().StartSyncAsync(projectId));

        Assert.Null(exception);
    }
}

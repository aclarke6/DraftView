using Moq;
using DraftView.Application.Services;
using DraftView.Domain.Entities;
using DraftView.Domain.Exceptions;
using DraftView.Domain.Interfaces.Repositories;
using DraftView.Domain.Interfaces.Services;

namespace DraftView.Application.Tests.Services;

/// <summary>
/// Tests for ProjectManagementService.AddDiscoveredProjectsAsync.
/// Covers: empty selection, new project creation, soft-deleted project restoration,
/// already-added and undiscovered UUIDs being ignored, DuplicateProjectException swallowing,
/// and AddedCount / SingleAddedProjectName result semantics.
/// Excludes: background sync execution (fire-and-forget via ISyncOrchestrationService), EF Core persistence.
/// </summary>
public class ProjectManagementServiceTests
{
    private readonly Mock<IProjectDiscoveryService>    _discoveryService        = new();
    private readonly Mock<IProjectRepository>          _projectRepo             = new();
    private readonly Mock<IUnitOfWork>                 _unitOfWork              = new();
    private readonly Mock<ISyncOrchestrationService>   _syncOrchestrationService = new();

    private ProjectManagementService CreateSut() => new(
        _discoveryService.Object,
        _projectRepo.Object,
        _unitOfWork.Object,
        _syncOrchestrationService.Object);

    [Fact]
    public async Task AddDiscoveredProjectsAsync_NoSelection_ReturnsZeroAdded()
    {
        var result = await CreateSut().AddDiscoveredProjectsAsync([], Guid.NewGuid());

        Assert.Equal(0, result.AddedCount);
        _projectRepo.Verify(r => r.AddAsync(It.IsAny<Project>(), default), Times.Never);
        _syncOrchestrationService.Verify(s => s.StartSyncAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AddDiscoveredProjectsAsync_NewProject_AddsAndReturnsName()
    {
        var authorId = Guid.NewGuid();
        var discovered = new DiscoveredProject
        {
            Name = "My Book", DropboxPath = "/path", SyncRootId = "uuid-1", AlreadyAdded = false
        };

        _discoveryService.Setup(d => d.DiscoverAsync(authorId, default))
            .ReturnsAsync(new List<DiscoveredProject> { discovered });
        _projectRepo.Setup(r => r.GetSoftDeletedBySyncRootIdAsync("uuid-1", default))
            .ReturnsAsync((Project?)null);

        var result = await CreateSut().AddDiscoveredProjectsAsync(["uuid-1"], authorId);

        Assert.Equal(1, result.AddedCount);
        Assert.Equal("My Book", result.SingleAddedProjectName);
        _projectRepo.Verify(r => r.AddAsync(It.Is<Project>(p => p.SyncRootId == "uuid-1"), default), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(default), Times.Once);
        _syncOrchestrationService.Verify(s => s.StartSyncAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddDiscoveredProjectsAsync_SoftDeletedProject_RestoresInsteadOfCreating()
    {
        var authorId = Guid.NewGuid();
        var discovered = new DiscoveredProject
        {
            Name = "Restored Book", DropboxPath = "/path", SyncRootId = "uuid-2", AlreadyAdded = false
        };
        var softDeleted = Project.Create("Old Name", "/path", authorId, "uuid-2");

        _discoveryService.Setup(d => d.DiscoverAsync(authorId, default))
            .ReturnsAsync(new List<DiscoveredProject> { discovered });
        _projectRepo.Setup(r => r.GetSoftDeletedBySyncRootIdAsync("uuid-2", default))
            .ReturnsAsync(softDeleted);

        var result = await CreateSut().AddDiscoveredProjectsAsync(["uuid-2"], authorId);

        Assert.Equal(1, result.AddedCount);
        Assert.Equal("Restored Book", softDeleted.Name);
        _projectRepo.Verify(r => r.AddAsync(It.IsAny<Project>(), default), Times.Never);
        _syncOrchestrationService.Verify(s => s.StartSyncAsync(softDeleted.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddDiscoveredProjectsAsync_AlreadyAddedProjects_AreIgnored()
    {
        var authorId = Guid.NewGuid();
        var discovered = new DiscoveredProject
        {
            Name = "Existing", DropboxPath = "/path", SyncRootId = "uuid-3", AlreadyAdded = true
        };

        _discoveryService.Setup(d => d.DiscoverAsync(authorId, default))
            .ReturnsAsync(new List<DiscoveredProject> { discovered });

        var result = await CreateSut().AddDiscoveredProjectsAsync(["uuid-3"], authorId);

        Assert.Equal(0, result.AddedCount);
        _projectRepo.Verify(r => r.AddAsync(It.IsAny<Project>(), default), Times.Never);
        _syncOrchestrationService.Verify(s => s.StartSyncAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AddDiscoveredProjectsAsync_UuidNotDiscovered_IsIgnored()
    {
        var authorId = Guid.NewGuid();
        _discoveryService.Setup(d => d.DiscoverAsync(authorId, default))
            .ReturnsAsync(new List<DiscoveredProject>());

        var result = await CreateSut().AddDiscoveredProjectsAsync(["missing-uuid"], authorId);

        Assert.Equal(0, result.AddedCount);
        _syncOrchestrationService.Verify(s => s.StartSyncAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AddDiscoveredProjectsAsync_DuplicateProjectException_IsSwallowedAndSkipped()
    {
        var authorId = Guid.NewGuid();
        var discovered = new DiscoveredProject
        {
            Name = "Dup Book", DropboxPath = "/path", SyncRootId = "uuid-4", AlreadyAdded = false
        };

        _discoveryService.Setup(d => d.DiscoverAsync(authorId, default))
            .ReturnsAsync(new List<DiscoveredProject> { discovered });
        _projectRepo.Setup(r => r.GetSoftDeletedBySyncRootIdAsync("uuid-4", default))
            .ReturnsAsync((Project?)null);
        _projectRepo.Setup(r => r.AddAsync(It.IsAny<Project>(), default))
            .ThrowsAsync(new DuplicateProjectException("uuid-4"));

        var result = await CreateSut().AddDiscoveredProjectsAsync(["uuid-4"], authorId);

        Assert.Equal(0, result.AddedCount);
        _syncOrchestrationService.Verify(s => s.StartSyncAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AddDiscoveredProjectsAsync_MultipleProjectsAdded_NameNotSetOnResult()
    {
        var authorId = Guid.NewGuid();
        var d1 = new DiscoveredProject { Name = "Book A", DropboxPath = "/a", SyncRootId = "uuid-a", AlreadyAdded = false };
        var d2 = new DiscoveredProject { Name = "Book B", DropboxPath = "/b", SyncRootId = "uuid-b", AlreadyAdded = false };

        _discoveryService.Setup(d => d.DiscoverAsync(authorId, default))
            .ReturnsAsync(new List<DiscoveredProject> { d1, d2 });
        _projectRepo.Setup(r => r.GetSoftDeletedBySyncRootIdAsync(It.IsAny<string>(), default))
            .ReturnsAsync((Project?)null);

        var result = await CreateSut().AddDiscoveredProjectsAsync(["uuid-a", "uuid-b"], authorId);

        Assert.Equal(2, result.AddedCount);
        Assert.Null(result.SingleAddedProjectName);
        _syncOrchestrationService.Verify(s => s.StartSyncAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    // -----------------------------------------------------------------------
    // SetActiveProjectAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task SetActiveProjectAsync_ProjectNotFound_ThrowsInvalidOperationException()
    {
        var projectId = Guid.NewGuid();
        _projectRepo.Setup(r => r.GetByIdAsync(projectId, default)).ReturnsAsync((Project?)null);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateSut().SetActiveProjectAsync(projectId));
    }

    [Fact]
    public async Task SetActiveProjectAsync_NoCurrentlyActive_ActivatesTargetAndSaves()
    {
        var authorId = Guid.NewGuid();
        var project = Project.Create("My Book", "/path", authorId, "uuid-1");
        _projectRepo.Setup(r => r.GetByIdAsync(project.Id, default)).ReturnsAsync(project);
        _projectRepo.Setup(r => r.GetReaderActiveProjectAsync(default)).ReturnsAsync((Project?)null);

        await CreateSut().SetActiveProjectAsync(project.Id);

        Assert.True(project.IsReaderActive);
        _unitOfWork.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task SetActiveProjectAsync_DifferentProjectCurrentlyActive_DeactivatesCurrentAndActivatesTarget()
    {
        var authorId = Guid.NewGuid();
        var current = Project.Create("Current", "/c", authorId, "uuid-c");
        current.ActivateForReaders();
        var next = Project.Create("Next", "/n", authorId, "uuid-n");

        _projectRepo.Setup(r => r.GetByIdAsync(next.Id, default)).ReturnsAsync(next);
        _projectRepo.Setup(r => r.GetReaderActiveProjectAsync(default)).ReturnsAsync(current);

        await CreateSut().SetActiveProjectAsync(next.Id);

        Assert.False(current.IsReaderActive);
        Assert.True(next.IsReaderActive);
        _unitOfWork.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task SetActiveProjectAsync_SameProjectAlreadyActive_JustActivatesAndSaves()
    {
        var authorId = Guid.NewGuid();
        var project = Project.Create("Book", "/path", authorId, "uuid");
        project.ActivateForReaders();

        _projectRepo.Setup(r => r.GetByIdAsync(project.Id, default)).ReturnsAsync(project);
        _projectRepo.Setup(r => r.GetReaderActiveProjectAsync(default)).ReturnsAsync(project);

        await CreateSut().SetActiveProjectAsync(project.Id);

        Assert.True(project.IsReaderActive);
        _unitOfWork.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    // -----------------------------------------------------------------------
    // DeactivateProjectAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task DeactivateProjectAsync_ProjectNotFound_ThrowsInvalidOperationException()
    {
        var projectId = Guid.NewGuid();
        _projectRepo.Setup(r => r.GetByIdAsync(projectId, default)).ReturnsAsync((Project?)null);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateSut().DeactivateProjectAsync(projectId));
    }

    [Fact]
    public async Task DeactivateProjectAsync_ProjectFound_DeactivatesAndSaves()
    {
        var authorId = Guid.NewGuid();
        var project = Project.Create("My Book", "/path", authorId, "uuid-1");
        project.ActivateForReaders();
        _projectRepo.Setup(r => r.GetByIdAsync(project.Id, default)).ReturnsAsync(project);

        await CreateSut().DeactivateProjectAsync(project.Id);

        Assert.False(project.IsReaderActive);
        _unitOfWork.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    // -----------------------------------------------------------------------
    // SoftDeleteProjectAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task SoftDeleteProjectAsync_ProjectNotFound_ThrowsInvalidOperationException()
    {
        var projectId = Guid.NewGuid();
        _projectRepo.Setup(r => r.GetByIdAsync(projectId, default)).ReturnsAsync((Project?)null);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateSut().SoftDeleteProjectAsync(projectId));
    }

    [Fact]
    public async Task SoftDeleteProjectAsync_ProjectFound_SoftDeletesAndSaves()
    {
        var authorId = Guid.NewGuid();
        var project = Project.Create("My Book", "/path", authorId, "uuid-1");
        _projectRepo.Setup(r => r.GetByIdAsync(project.Id, default)).ReturnsAsync(project);

        await CreateSut().SoftDeleteProjectAsync(project.Id);

        Assert.True(project.IsSoftDeleted);
        _unitOfWork.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task AddDiscoveredProjectsAsync_FirstProjectDuplicate_SecondSucceeds_NameIsSecondProject()
    {
        var authorId = Guid.NewGuid();
        var d1 = new DiscoveredProject { Name = "Dup Book", DropboxPath = "/a", SyncRootId = "uuid-dup", AlreadyAdded = false };
        var d2 = new DiscoveredProject { Name = "Good Book", DropboxPath = "/b", SyncRootId = "uuid-good", AlreadyAdded = false };

        _discoveryService.Setup(d => d.DiscoverAsync(authorId, default))
            .ReturnsAsync(new List<DiscoveredProject> { d1, d2 });
        _projectRepo.Setup(r => r.GetSoftDeletedBySyncRootIdAsync(It.IsAny<string>(), default))
            .ReturnsAsync((Project?)null);
        _projectRepo.SetupSequence(r => r.AddAsync(It.IsAny<Project>(), default))
            .ThrowsAsync(new DuplicateProjectException("uuid-dup"))
            .Returns(Task.CompletedTask);

        var result = await CreateSut().AddDiscoveredProjectsAsync(["uuid-dup", "uuid-good"], authorId);

        Assert.Equal(1, result.AddedCount);
        Assert.Equal("Good Book", result.SingleAddedProjectName);
        _syncOrchestrationService.Verify(s => s.StartSyncAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}

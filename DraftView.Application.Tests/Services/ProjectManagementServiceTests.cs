using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using DraftView.Application.Services;
using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;
using DraftView.Domain.Exceptions;
using DraftView.Domain.Interfaces.Repositories;
using DraftView.Domain.Interfaces.Services;

namespace DraftView.Application.Tests.Services;

public class ProjectManagementServiceTests
{
    private readonly Mock<IProjectDiscoveryService> _discoveryService = new();
    private readonly Mock<IProjectRepository>        _projectRepo     = new();
    private readonly Mock<IUnitOfWork>               _unitOfWork      = new();
    private readonly Mock<IServiceScopeFactory>      _scopeFactory    = new();
    private readonly Mock<ISyncService>              _syncService     = new();

    private ProjectManagementService CreateSut() => new(
        _discoveryService.Object,
        _projectRepo.Object,
        _unitOfWork.Object,
        _scopeFactory.Object,
        NullLogger<ProjectManagementService>.Instance);

    private void SetUpBackgroundScope()
    {
        var serviceProvider = new Mock<IServiceProvider>();
        serviceProvider.Setup(p => p.GetService(typeof(ISyncService))).Returns(_syncService.Object);
        serviceProvider.Setup(p => p.GetService(typeof(IProjectRepository))).Returns(_projectRepo.Object);
        serviceProvider.Setup(p => p.GetService(typeof(IUnitOfWork))).Returns(_unitOfWork.Object);

        var scope = new Mock<IServiceScope>();
        scope.Setup(s => s.ServiceProvider).Returns(serviceProvider.Object);
        _scopeFactory.Setup(f => f.CreateScope()).Returns(scope.Object);
    }

    [Fact]
    public async Task AddDiscoveredProjectsAsync_NoSelection_ReturnsZeroAdded()
    {
        var result = await CreateSut().AddDiscoveredProjectsAsync([], Guid.NewGuid());

        Assert.Equal(0, result.AddedCount);
        _projectRepo.Verify(r => r.AddAsync(It.IsAny<Project>(), default), Times.Never);
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
        _projectRepo.Setup(r => r.GetAllAsync(default))
            .ReturnsAsync(new List<Project> { Project.Create("My Book", "/path", authorId, "uuid-1") });
        SetUpBackgroundScope();

        var result = await CreateSut().AddDiscoveredProjectsAsync(["uuid-1"], authorId);

        Assert.Equal(1, result.AddedCount);
        Assert.Equal("My Book", result.SingleAddedProjectName);
        _projectRepo.Verify(r => r.AddAsync(It.Is<Project>(p => p.SyncRootId == "uuid-1"), default), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(default), Times.AtLeastOnce);
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
        _projectRepo.Setup(r => r.GetAllAsync(default))
            .ReturnsAsync(new List<Project> { softDeleted });
        SetUpBackgroundScope();

        var result = await CreateSut().AddDiscoveredProjectsAsync(["uuid-2"], authorId);

        Assert.Equal(1, result.AddedCount);
        Assert.Equal("Restored Book", softDeleted.Name);
        _projectRepo.Verify(r => r.AddAsync(It.IsAny<Project>(), default), Times.Never);
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
    }

    [Fact]
    public async Task AddDiscoveredProjectsAsync_UuidNotDiscovered_IsIgnored()
    {
        var authorId = Guid.NewGuid();
        _discoveryService.Setup(d => d.DiscoverAsync(authorId, default))
            .ReturnsAsync(new List<DiscoveredProject>());

        var result = await CreateSut().AddDiscoveredProjectsAsync(["missing-uuid"], authorId);

        Assert.Equal(0, result.AddedCount);
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
        _projectRepo.Setup(r => r.GetAllAsync(default))
            .ReturnsAsync(new List<Project>());

        var result = await CreateSut().AddDiscoveredProjectsAsync(["uuid-4"], authorId);

        Assert.Equal(0, result.AddedCount);
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
        _projectRepo.Setup(r => r.GetAllAsync(default))
            .ReturnsAsync(new List<Project>
            {
                Project.Create("Book A", "/a", authorId, "uuid-a"),
                Project.Create("Book B", "/b", authorId, "uuid-b")
            });
        SetUpBackgroundScope();

        var result = await CreateSut().AddDiscoveredProjectsAsync(["uuid-a", "uuid-b"], authorId);

        Assert.Equal(2, result.AddedCount);
        Assert.Null(result.SingleAddedProjectName);
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
        _projectRepo.Setup(r => r.GetAllAsync(default))
            .ReturnsAsync(new List<Project> { Project.Create("Good Book", "/b", authorId, "uuid-good") });

        var result = await CreateSut().AddDiscoveredProjectsAsync(["uuid-dup", "uuid-good"], authorId);

        Assert.Equal(1, result.AddedCount);
        Assert.Equal("Good Book", result.SingleAddedProjectName);
    }
}

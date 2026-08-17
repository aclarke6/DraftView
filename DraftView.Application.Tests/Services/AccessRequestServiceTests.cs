using Moq;
using DraftView.Application.Services;
using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;
using DraftView.Domain.Exceptions;
using DraftView.Domain.Interfaces.Repositories;
using DraftView.Domain.Interfaces.Services;
using DraftView.Domain.Notifications;

namespace DraftView.Application.Tests.Services;

public class AccessRequestServiceTests
{
    private static readonly Guid AuthorId  = Guid.NewGuid();
    private static readonly Guid ReaderId  = Guid.NewGuid();
    private static readonly Guid ProjectId = Guid.NewGuid();

    private readonly Mock<IAccessRequestRepository>      _accessRequestRepo = new();
    private readonly Mock<IProjectRepository>            _projectRepo       = new();
    private readonly Mock<IUserRepository>               _userRepo          = new();
    private readonly Mock<IReaderAccessRepository>       _readerAccessRepo  = new();
    private readonly Mock<IAuthorNotificationRepository> _notificationRepo  = new();
    private readonly Mock<IUnitOfWork>                   _unitOfWork        = new();
    private readonly Mock<IEmailSender>                  _emailSender       = new();

    private AccessRequestService CreateSut() => new(
        _accessRequestRepo.Object,
        _projectRepo.Object,
        _userRepo.Object,
        _readerAccessRepo.Object,
        _notificationRepo.Object,
        _unitOfWork.Object,
        _emailSender.Object);

    private static Project MakeOpenProject()
    {
        var p = Project.Create("My Novel", "/dropbox/test.scriv", AuthorId);
        p.Open("A gripping thriller.");
        return p;
    }

    private static Project MakeClosedProject()
    {
        return Project.Create("My Novel", "/dropbox/test.scriv", AuthorId);
    }

    private static User MakeReader() =>
        User.Create("reader@example.com", "Test Reader", Role.BetaReader);

    private static User MakeAuthor() =>
        User.Create("author@example.com", "Test Author", Role.Author);

    private static AccessRequest MakePendingRequest() =>
        AccessRequest.Create(ReaderId, ProjectId, null, null);

    // -----------------------------------------------------------------------
    // SubmitRequestAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task SubmitRequestAsync_WhenProjectIsOpen_CreatesRequest()
    {
        var project = MakeOpenProject();
        _projectRepo.Setup(r => r.GetByIdAsync(ProjectId, default)).ReturnsAsync(project);
        _accessRequestRepo.Setup(r => r.GetPendingByProjectIdAsync(ProjectId, default))
            .ReturnsAsync([]);
        _userRepo.Setup(r => r.GetAuthorAsync(default)).ReturnsAsync(MakeAuthor());

        AccessRequest? added = null;
        _accessRequestRepo
            .Setup(r => r.AddAsync(It.IsAny<AccessRequest>(), default))
            .Callback<AccessRequest, CancellationToken>((req, _) => added = req);

        var sut = CreateSut();
        await sut.SubmitRequestAsync(ReaderId, ProjectId, "Love this genre", "r@x.com");

        Assert.NotNull(added);
        Assert.Equal(ReaderId, added!.ReaderId);
        Assert.Equal(ProjectId, added.ProjectId);
        Assert.Equal("Love this genre", added.CoverNote);
        Assert.Equal(AccessRequestStatus.Pending, added.Status);
    }

    [Fact]
    public async Task SubmitRequestAsync_WhenProjectIsClosed_Throws()
    {
        var project = MakeClosedProject();
        _projectRepo.Setup(r => r.GetByIdAsync(ProjectId, default)).ReturnsAsync(project);

        var sut = CreateSut();
        var ex = await Assert.ThrowsAsync<InvariantViolationException>(
            () => sut.SubmitRequestAsync(ReaderId, ProjectId, null, null));

        Assert.Equal("I-AR-CLOSED", ex.InvariantCode);
    }

    [Fact]
    public async Task SubmitRequestAsync_WhenProjectNotFound_Throws()
    {
        _projectRepo.Setup(r => r.GetByIdAsync(ProjectId, default)).ReturnsAsync((Project?)null);

        var sut = CreateSut();
        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => sut.SubmitRequestAsync(ReaderId, ProjectId, null, null));
    }

    [Fact]
    public async Task SubmitRequestAsync_WhenAlreadyHasPendingRequest_Throws()
    {
        var project = MakeOpenProject();
        var existing = AccessRequest.Create(ReaderId, ProjectId, null, null);

        _projectRepo.Setup(r => r.GetByIdAsync(ProjectId, default)).ReturnsAsync(project);
        _accessRequestRepo.Setup(r => r.GetPendingByProjectIdAsync(ProjectId, default))
            .ReturnsAsync([existing]);

        var sut = CreateSut();
        var ex = await Assert.ThrowsAsync<InvariantViolationException>(
            () => sut.SubmitRequestAsync(ReaderId, ProjectId, null, null));

        Assert.Equal("I-AR-DUP", ex.InvariantCode);
    }

    [Fact]
    public async Task SubmitRequestAsync_CreatesAuthorNotification()
    {
        var project = MakeOpenProject();
        var author = MakeAuthor();
        _projectRepo.Setup(r => r.GetByIdAsync(ProjectId, default)).ReturnsAsync(project);
        _accessRequestRepo.Setup(r => r.GetPendingByProjectIdAsync(ProjectId, default))
            .ReturnsAsync([]);
        _userRepo.Setup(r => r.GetAuthorAsync(default)).ReturnsAsync(author);

        AuthorNotification? notification = null;
        _notificationRepo
            .Setup(r => r.AddAsync(It.IsAny<AuthorNotification>(), default))
            .Callback<AuthorNotification, CancellationToken>((n, _) => notification = n);

        var sut = CreateSut();
        await sut.SubmitRequestAsync(ReaderId, ProjectId, null, null);

        Assert.NotNull(notification);
        Assert.Equal(author.Id, notification!.AuthorId);
        Assert.Equal(NotificationEventType.AccessRequest, notification.EventType);
    }

    [Fact]
    public async Task SubmitRequestAsync_SavesChanges()
    {
        var project = MakeOpenProject();
        _projectRepo.Setup(r => r.GetByIdAsync(ProjectId, default)).ReturnsAsync(project);
        _accessRequestRepo.Setup(r => r.GetPendingByProjectIdAsync(ProjectId, default))
            .ReturnsAsync([]);
        _userRepo.Setup(r => r.GetAuthorAsync(default)).ReturnsAsync(MakeAuthor());

        var sut = CreateSut();
        await sut.SubmitRequestAsync(ReaderId, ProjectId, null, null);

        _unitOfWork.Verify(u => u.SaveChangesAsync(default), Times.AtLeastOnce);
    }

    // -----------------------------------------------------------------------
    // ApproveRequestAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ApproveRequestAsync_WhenAuthorOwnsProject_GrantsReaderAccess()
    {
        var request = MakePendingRequest();
        var project = MakeOpenProject();
        var reader  = MakeReader();

        _accessRequestRepo.Setup(r => r.GetByIdAsync(request.Id, default)).ReturnsAsync(request);
        _projectRepo.Setup(r => r.GetByIdAsync(ProjectId, default)).ReturnsAsync(project);
        _userRepo.Setup(r => r.GetByIdAsync(ReaderId, default)).ReturnsAsync(reader);
        _readerAccessRepo.Setup(r => r.GetByReaderAndProjectAsync(ReaderId, ProjectId, default))
            .ReturnsAsync((ReaderAccess?)null);

        ReaderAccess? granted = null;
        _readerAccessRepo
            .Setup(r => r.AddAsync(It.IsAny<ReaderAccess>(), default))
            .Callback<ReaderAccess, CancellationToken>((ra, _) => granted = ra);

        var sut = CreateSut();
        await sut.ApproveRequestAsync(request.Id, AuthorId);

        Assert.NotNull(granted);
        Assert.Equal(ReaderId, granted!.ReaderId);
        Assert.Equal(ProjectId, granted.ProjectId);
    }

    [Fact]
    public async Task ApproveRequestAsync_MarksRequestApproved()
    {
        var request = MakePendingRequest();
        var project = MakeOpenProject();
        var reader  = MakeReader();

        _accessRequestRepo.Setup(r => r.GetByIdAsync(request.Id, default)).ReturnsAsync(request);
        _projectRepo.Setup(r => r.GetByIdAsync(ProjectId, default)).ReturnsAsync(project);
        _userRepo.Setup(r => r.GetByIdAsync(ReaderId, default)).ReturnsAsync(reader);
        _readerAccessRepo.Setup(r => r.GetByReaderAndProjectAsync(ReaderId, ProjectId, default))
            .ReturnsAsync((ReaderAccess?)null);

        var sut = CreateSut();
        await sut.ApproveRequestAsync(request.Id, AuthorId);

        Assert.Equal(AccessRequestStatus.Approved, request.Status);
        Assert.NotNull(request.RespondedAt);
    }

    [Fact]
    public async Task ApproveRequestAsync_SendsEmailToReader()
    {
        var request = MakePendingRequest();
        var project = MakeOpenProject();
        var reader  = MakeReader();

        _accessRequestRepo.Setup(r => r.GetByIdAsync(request.Id, default)).ReturnsAsync(request);
        _projectRepo.Setup(r => r.GetByIdAsync(ProjectId, default)).ReturnsAsync(project);
        _userRepo.Setup(r => r.GetByIdAsync(ReaderId, default)).ReturnsAsync(reader);
        _readerAccessRepo.Setup(r => r.GetByReaderAndProjectAsync(ReaderId, ProjectId, default))
            .ReturnsAsync((ReaderAccess?)null);

        var sut = CreateSut();
        await sut.ApproveRequestAsync(request.Id, AuthorId);

        _emailSender.Verify(e => e.SendAsync(
            reader.Email,
            reader.DisplayName,
            It.IsAny<string>(),
            It.Is<string>(body => body.Contains(project.Name)),
            default), Times.Once);
    }

    [Fact]
    public async Task ApproveRequestAsync_WhenAuthorDoesNotOwnProject_Throws()
    {
        var wrongAuthorId = Guid.NewGuid();
        var request = MakePendingRequest();
        var project = MakeOpenProject();

        _accessRequestRepo.Setup(r => r.GetByIdAsync(request.Id, default)).ReturnsAsync(request);
        _projectRepo.Setup(r => r.GetByIdAsync(ProjectId, default)).ReturnsAsync(project);

        var sut = CreateSut();
        var ex = await Assert.ThrowsAsync<UnauthorisedOperationException>(
            () => sut.ApproveRequestAsync(request.Id, wrongAuthorId));

        Assert.NotNull(ex);
    }

    [Fact]
    public async Task ApproveRequestAsync_WhenRequestNotFound_Throws()
    {
        _accessRequestRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), default))
            .ReturnsAsync((AccessRequest?)null);

        var sut = CreateSut();
        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => sut.ApproveRequestAsync(Guid.NewGuid(), AuthorId));
    }

    [Fact]
    public async Task ApproveRequestAsync_WhenReaderAlreadyHasActiveAccess_DoesNotDuplicateGrant()
    {
        var request = MakePendingRequest();
        var project = MakeOpenProject();
        var reader  = MakeReader();
        var existing = ReaderAccess.Grant(ReaderId, AuthorId, ProjectId);

        _accessRequestRepo.Setup(r => r.GetByIdAsync(request.Id, default)).ReturnsAsync(request);
        _projectRepo.Setup(r => r.GetByIdAsync(ProjectId, default)).ReturnsAsync(project);
        _userRepo.Setup(r => r.GetByIdAsync(ReaderId, default)).ReturnsAsync(reader);
        _readerAccessRepo.Setup(r => r.GetByReaderAndProjectAsync(ReaderId, ProjectId, default))
            .ReturnsAsync(existing);

        var sut = CreateSut();
        await sut.ApproveRequestAsync(request.Id, AuthorId);

        _readerAccessRepo.Verify(r => r.AddAsync(It.IsAny<ReaderAccess>(), default), Times.Never);
    }

    // -----------------------------------------------------------------------
    // DeclineRequestAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task DeclineRequestAsync_WhenAuthorOwnsProject_MarksDeclined()
    {
        var request = MakePendingRequest();
        var project = MakeOpenProject();

        _accessRequestRepo.Setup(r => r.GetByIdAsync(request.Id, default)).ReturnsAsync(request);
        _projectRepo.Setup(r => r.GetByIdAsync(ProjectId, default)).ReturnsAsync(project);

        var sut = CreateSut();
        await sut.DeclineRequestAsync(request.Id, AuthorId);

        Assert.Equal(AccessRequestStatus.Declined, request.Status);
        Assert.NotNull(request.RespondedAt);
    }

    [Fact]
    public async Task DeclineRequestAsync_DoesNotSendEmail()
    {
        var request = MakePendingRequest();
        var project = MakeOpenProject();

        _accessRequestRepo.Setup(r => r.GetByIdAsync(request.Id, default)).ReturnsAsync(request);
        _projectRepo.Setup(r => r.GetByIdAsync(ProjectId, default)).ReturnsAsync(project);

        var sut = CreateSut();
        await sut.DeclineRequestAsync(request.Id, AuthorId);

        _emailSender.Verify(
            e => e.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), default),
            Times.Never);
    }

    [Fact]
    public async Task DeclineRequestAsync_WhenAuthorDoesNotOwnProject_Throws()
    {
        var wrongAuthorId = Guid.NewGuid();
        var request = MakePendingRequest();
        var project = MakeOpenProject();

        _accessRequestRepo.Setup(r => r.GetByIdAsync(request.Id, default)).ReturnsAsync(request);
        _projectRepo.Setup(r => r.GetByIdAsync(ProjectId, default)).ReturnsAsync(project);

        var sut = CreateSut();
        await Assert.ThrowsAsync<UnauthorisedOperationException>(
            () => sut.DeclineRequestAsync(request.Id, wrongAuthorId));
    }

    [Fact]
    public async Task DeclineRequestAsync_WhenRequestNotFound_Throws()
    {
        _accessRequestRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), default))
            .ReturnsAsync((AccessRequest?)null);

        var sut = CreateSut();
        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => sut.DeclineRequestAsync(Guid.NewGuid(), AuthorId));
    }

    // -----------------------------------------------------------------------
    // BulkDeclineOnRevokeAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task BulkDeclineOnRevokeAsync_CallsBulkDeclineOnRepo()
    {
        var sut = CreateSut();
        await sut.BulkDeclineOnRevokeAsync(ProjectId);

        _accessRequestRepo.Verify(
            r => r.BulkDeclineByProjectAsync(ProjectId, It.IsAny<DateTime>(), default),
            Times.Once);
    }

    [Fact]
    public async Task BulkDeclineOnRevokeAsync_SavesChanges()
    {
        var sut = CreateSut();
        await sut.BulkDeclineOnRevokeAsync(ProjectId);

        _unitOfWork.Verify(u => u.SaveChangesAsync(default), Times.AtLeastOnce);
    }
}

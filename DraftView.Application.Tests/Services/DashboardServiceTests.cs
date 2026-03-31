using Moq;
using DraftView.Application.Services;
using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;
using DraftView.Domain.Interfaces.Repositories;

namespace DraftView.Application.Tests.Services;

public class DashboardServiceTests
{
    private readonly Mock<ISectionRepository>             _sectionRepo  = new();
    private readonly Mock<IUserRepository>                _userRepo     = new();
    private readonly Mock<IEmailDeliveryLogRepository>    _logRepo      = new();
    private readonly Mock<ICommentRepository>             _commentRepo  = new();
    private readonly Mock<IInvitationRepository>          _invRepo      = new();
    private readonly Mock<IScrivenerProjectRepository>    _projectRepo  = new();

    private DashboardService CreateSut() => new(
        _sectionRepo.Object,
        _userRepo.Object,
        _logRepo.Object,
        _commentRepo.Object,
        _invRepo.Object,
        _projectRepo.Object);

    [Fact]
    public async Task GetProjectOverviewAsync_ReturnsSections()
    {
        var projectId = Guid.NewGuid();
        var section   = Section.CreateDocument(projectId, "UUID-1", "Scene 1",
            null, 0, "<p>x</p>", "h", "First Draft");
        section.PublishAsPartOfChapter("h");
        var sut = CreateSut();

        _sectionRepo.Setup(r => r.GetPublishedByProjectIdAsync(projectId, default))
            .ReturnsAsync(new List<Section> { section });

        var result = await sut.GetProjectOverviewAsync(projectId);

        Assert.Single(result);
    }

    [Fact]
    public async Task GetReaderSummaryAsync_ReturnsBetaReaders()
    {
        var reader = User.Create("reader@example.com", "Reader", Role.BetaReader);
        reader.Activate();
        var sut = CreateSut();

        _userRepo.Setup(r => r.GetAllBetaReadersAsync(default))
            .ReturnsAsync(new List<User> { reader });

        var result = await sut.GetReaderSummaryAsync();

        Assert.Single(result);
    }

    [Fact]
    public async Task GetEmailHealthSummaryAsync_ReturnsFailedLogs()
    {
        var log = EmailDeliveryLog.Create(Guid.NewGuid(), "test@example.com",
            EmailType.Invitation, null);
        log.RecordAttempt(false, "Timeout.");
        log.MarkFailed();
        var sut = CreateSut();

        _logRepo.Setup(r => r.GetFailedAsync(default))
            .ReturnsAsync(new List<EmailDeliveryLog> { log });

        var result = await sut.GetEmailHealthSummaryAsync();

        Assert.Single(result);
        Assert.Equal(EmailStatus.Failed, result[0].Status);
    }
}


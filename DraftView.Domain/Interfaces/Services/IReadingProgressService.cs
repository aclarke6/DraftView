using DraftView.Domain.Contracts;
using DraftView.Domain.Entities;

namespace DraftView.Domain.Interfaces.Services;

public interface IReadingProgressService
{
    Task RecordOpenAsync(Guid sectionId, Guid userId, CancellationToken ct = default);
    Task<bool> IsCaughtUpAsync(Guid userId, Guid projectId, CancellationToken ct = default);
    Task<bool> HasReadSectionAsync(Guid userId, Guid sectionId, CancellationToken ct = default);
    Task<IReadOnlyList<ReadEvent>> GetProgressForProjectAsync(Guid projectId, CancellationToken ct = default);
    Task<ReadEvent?> GetLastReadEventAsync(Guid userId, Guid projectId, CancellationToken ct = default);
    Task<ReadEvent?> GetLastReadEventAcrossProjectsAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Marks a scene as read: sets IsRead=true on the ReadEvent and captures a
    /// ReaderSnapshot of the current content as the reader's new baseline.
    /// No-op if no ReadEvent exists (reader has not opened the scene).
    /// </summary>
    Task MarkReadAsync(Guid sectionId, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Marks a scene as unread: sets IsRead=false on the ReadEvent.
    /// The snapshot is preserved so the change state can still be computed.
    /// No-op if no ReadEvent exists.
    /// </summary>
    Task MarkUnreadAsync(Guid sectionId, Guid userId, CancellationToken ct = default);

    Task CaptureResumePositionAsync(
        CaptureResumePositionRequest request,
        Guid userId,
        CancellationToken ct = default);

    Task<ResumeRestoreTargetDto?> GetResumeRestoreTargetAsync(
        Guid sectionId,
        Guid userId,
        CancellationToken ct = default);
}

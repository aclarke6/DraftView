using DraftView.Domain.Entities;

namespace DraftView.Domain.Interfaces.Services;

public interface IPublicationService
{
    /// <summary>
    /// Publishes a chapter folder and all its descendant scenes.
    /// All scenes must have a publishable status (First Draft or above).
    /// Throws if any scene is below First Draft.
    /// </summary>
    Task PublishChapterAsync(Guid chapterId, Guid authorId, CancellationToken ct = default);

    /// <summary>
    /// Unpublishes a chapter folder and all its descendant scenes.
    /// </summary>
    Task UnpublishChapterAsync(Guid chapterId, Guid authorId, CancellationToken ct = default);

    /// <summary>
    /// Returns all chapter folders that are currently published.
    /// </summary>
    Task<IReadOnlyList<Section>> GetPublishedChaptersAsync(Guid projectId, CancellationToken ct = default);

    /// <summary>
    /// Returns all chapter folders eligible for publishing
    /// (all descendant scenes are First Draft or above).
    /// </summary>
    Task<IReadOnlyList<Section>> GetPublishableChaptersAsync(Guid projectId, CancellationToken ct = default);

    /// <summary>
    /// Returns whether a given folder can be published
    /// (all descendant scenes are publishable status).
    /// </summary>
    Task<bool> CanPublishAsync(Guid folderId, CancellationToken ct = default);
}

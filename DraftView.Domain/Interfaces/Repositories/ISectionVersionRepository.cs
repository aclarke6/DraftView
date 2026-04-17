namespace DraftView.Domain.Interfaces.Repositories;

/// <summary>
/// Repository contract for SectionVersion persistence.
/// </summary>
public interface ISectionVersionRepository
{
    /// <summary>Returns the highest VersionNumber for a section, or 0 if none exist.</summary>
    Task<int> GetMaxVersionNumberAsync(Guid sectionId, CancellationToken ct = default);

    /// <summary>Returns the latest SectionVersion for a section, or null if none exist.</summary>
    Task<Domain.Entities.SectionVersion?> GetLatestAsync(Guid sectionId, CancellationToken ct = default);

    /// <summary>Returns all versions for a section, ordered by VersionNumber ascending.</summary>
    Task<IReadOnlyList<Domain.Entities.SectionVersion>> GetAllBySectionIdAsync(Guid sectionId, CancellationToken ct = default);

    Task AddAsync(Domain.Entities.SectionVersion version, CancellationToken ct = default);
}

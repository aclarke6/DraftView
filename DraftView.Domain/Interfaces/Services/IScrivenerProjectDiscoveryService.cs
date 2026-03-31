namespace DraftView.Domain.Interfaces.Services;

/// <summary>
/// Represents a project discovered from a Dropbox vault scan,
/// before it has been added to the database.
/// </summary>
public sealed class DiscoveredProject
{
    /// <summary>Display name derived from vault and book structure.</summary>
    public string Name { get; init; } = default!;

    /// <summary>Dropbox path to the .scriv folder (e.g. /Apps/Scrivener/My Novel.scriv).</summary>
    public string DropboxPath { get; init; } = default!;

    /// <summary>
    /// UUID of the binder node that is the root of this project.
    /// For book-split vaults, the Book folder UUID.
    /// For single-project vaults, the ManuscriptRoot UUID.
    /// </summary>
    public string ScrivenerRootUuid { get; init; } = default!;

    /// <summary>True if this project is already in the database.</summary>
    public bool AlreadyAdded { get; init; }
}

public interface IScrivenerProjectDiscoveryService
{
    /// <summary>
    /// Scans the configured Dropbox path for .scriv vaults, parses each one,
    /// applies book-split detection, and returns all discovered projects
    /// (including those already added, flagged accordingly).
    /// </summary>
    Task<IReadOnlyList<DiscoveredProject>> DiscoverAsync(CancellationToken ct = default);
}

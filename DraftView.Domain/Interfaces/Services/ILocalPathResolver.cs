using DraftView.Domain.Entities;

namespace DraftView.Domain.Interfaces.Services;

public interface ILocalPathResolver
{
    /// <summary>
    /// Returns the local filesystem path for a project's .scriv folder.
    /// For local projects this is the DropboxPath directly.
    /// For Dropbox-backed projects this is the local cache path.
    /// </summary>
    Task<string> ResolveAsync(ScrivenerProject project, CancellationToken ct = default);

    /// <summary>
    /// Returns the path to the .scrivx file within the resolved local path.
    /// </summary>
    Task<string> ResolveScrivxAsync(ScrivenerProject project, CancellationToken ct = default);
}

namespace DraftView.Domain.Interfaces.Services;

public sealed class DiscoveredProject
{
    public string Name { get; init; } = default!;
    public string DropboxPath { get; init; } = default!;
    public string ScrivenerRootUuid { get; init; } = default!;
    public bool AlreadyAdded { get; init; }
}

public interface IScrivenerProjectDiscoveryService
{
    Task<IReadOnlyList<DiscoveredProject>> DiscoverAsync(
        Guid userId, CancellationToken ct = default);
}

namespace ScrivenerSync.Domain.Interfaces.Services;

public sealed class RtfConversionResult
{
    public string Html { get; init; } = default!;
    public string Hash { get; init; } = default!;
}

public interface IRtfConverter
{
    string GetContentPath(string scrivFolderPath, string uuid);
    Task<RtfConversionResult?> ConvertAsync(string scrivFolderPath, string uuid, CancellationToken ct = default);
}

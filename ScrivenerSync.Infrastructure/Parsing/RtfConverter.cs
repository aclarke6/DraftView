using System.Security.Cryptography;
using System.Text;
using RtfPipe;
using ScrivenerSync.Domain.Interfaces.Services;

namespace ScrivenerSync.Infrastructure.Parsing;

public class RtfConverter : IRtfConverter
{
    static RtfConverter()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public string GetContentPath(string scrivFolderPath, string uuid) =>
        Path.Combine(scrivFolderPath, "Files", "Data", uuid, "content.rtf");

    public async Task<RtfConversionResult?> ConvertAsync(
        string scrivFolderPath,
        string uuid,
        CancellationToken ct = default)
    {
        var path = GetContentPath(scrivFolderPath, uuid);

        if (!File.Exists(path))
            return null;

        var rtfBytes = await File.ReadAllBytesAsync(path, ct);

        if (rtfBytes.Length == 0)
            return null;

        var html = ConvertRtfToHtml(rtfBytes);
        var hash = ComputeHash(rtfBytes);

        return new RtfConversionResult
        {
            Html = html,
            Hash = hash
        };
    }

    private static string ConvertRtfToHtml(byte[] rtfBytes)
    {
        var rtfText = Encoding.UTF8.GetString(rtfBytes);
        return Rtf.ToHtml(rtfText);
    }

    private static string ComputeHash(byte[] content)
    {
        var hashBytes = SHA256.HashData(content);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}

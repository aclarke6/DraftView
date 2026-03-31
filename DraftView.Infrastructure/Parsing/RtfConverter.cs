using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using RtfPipe;
using DraftView.Domain.Interfaces.Services;

namespace DraftView.Infrastructure.Parsing;

public class RtfConverter : IRtfConverter
{
    private static readonly Regex ScrivCharStyleOpen  = new(@"<\$Scr_Cs::\d+>",   RegexOptions.Compiled);
    private static readonly Regex ScrivCharStyleClose = new(@"</\$Scr_Cs::\d+>",  RegexOptions.Compiled);
    private static readonly Regex ScrivParaStyleOpen  = new(@"<\$Scr_Ps::\d+>",   RegexOptions.Compiled);
    private static readonly Regex ScrivParaStyleClose = new(@"<[!/]\$Scr_Ps::\d+>", RegexOptions.Compiled);

    static RtfConverter()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public string GetContentPath(string scrivFolderPath, string uuid) =>
        Path.Combine(scrivFolderPath, "Files", "Data", uuid, "content.rtf");

    public async Task<RtfConversionResult?> ConvertAsync(
        string scrivFolderPath, string uuid, CancellationToken ct = default)
    {
        var path = GetContentPath(scrivFolderPath, uuid);
        if (!File.Exists(path)) return null;

        var rtfBytes = await File.ReadAllBytesAsync(path, ct);
        if (rtfBytes.Length == 0) return null;

        return new RtfConversionResult { Html = ConvertRtfToHtml(rtfBytes), Hash = ComputeHash(rtfBytes) };
    }

    private static string ConvertRtfToHtml(byte[] rtfBytes)
    {
        var rtfText = Encoding.UTF8.GetString(rtfBytes);
        rtfText = ScrivCharStyleOpen.Replace(rtfText, string.Empty);
        rtfText = ScrivCharStyleClose.Replace(rtfText, string.Empty);
        rtfText = ScrivParaStyleOpen.Replace(rtfText, string.Empty);
        rtfText = ScrivParaStyleClose.Replace(rtfText, string.Empty);
        return Rtf.ToHtml(rtfText);
    }

    private static string ComputeHash(byte[] content) =>
        Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
}

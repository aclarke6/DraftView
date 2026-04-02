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

    public string GetContentPath(string scrivFolderPath, string uuid)
    {
        var baseDir = new DirectoryInfo(scrivFolderPath);
        var filesDir = baseDir.GetDirectories()
            .FirstOrDefault(d => d.Name.Equals("files", StringComparison.OrdinalIgnoreCase));
        if (filesDir is null) return Path.Combine(scrivFolderPath, "Files", "Data", uuid, "content.rtf");
        var dataDir = filesDir.GetDirectories()
            .FirstOrDefault(d => d.Name.Equals("data", StringComparison.OrdinalIgnoreCase));
        if (dataDir is null) return Path.Combine(filesDir.FullName, "Data", uuid, "content.rtf");
        var uuidDir = dataDir.GetDirectories()
            .FirstOrDefault(d => d.Name.Equals(uuid, StringComparison.OrdinalIgnoreCase));
        if (uuidDir is null) return Path.Combine(dataDir.FullName, uuid, "content.rtf");
        var contentFile = uuidDir.GetFiles()
            .FirstOrDefault(f => f.Name.Equals("content.rtf", StringComparison.OrdinalIgnoreCase));
        return contentFile?.FullName ?? Path.Combine(uuidDir.FullName, "content.rtf");
    }

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
        var html = Rtf.ToHtml(rtfText);
        html = Regex.Replace(html, @" style=""[^""]*""", string.Empty, RegexOptions.None);
        return html;
    }

    private static string ComputeHash(byte[] content) =>
        Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
}


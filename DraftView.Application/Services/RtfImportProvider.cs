using System.Text;
using DraftView.Domain.Exceptions;
using DraftView.Domain.Interfaces.Services;

namespace DraftView.Application.Services;

/// <summary>
/// Converts RTF uploads to HTML by delegating to the existing RtfConverter.
/// </summary>
public class RtfImportProvider(IRtfConverter rtfConverter) : IImportProvider
{
    public string SupportedExtension => ".rtf";
    public string ProviderName => "RTF";

    /// <summary>
    /// Converts the supplied RTF stream to HTML using a temporary filesystem location.
    /// </summary>
    public async Task<string> ConvertToHtmlAsync(
        Stream fileStream,
        CancellationToken cancellationToken = default)
    {
        var uuid = Guid.NewGuid().ToString("N");
        var tempFolder = Path.Combine(Path.GetTempPath(), "DraftView", "RtfImport", uuid);

        try
        {
            Directory.CreateDirectory(tempFolder);

            var tempFilePath = Path.Combine(tempFolder, $"{uuid}.rtf");
            await using (var tempFile = File.Create(tempFilePath))
            {
                await fileStream.CopyToAsync(tempFile, cancellationToken);
            }

            var compatFolder = Path.Combine(tempFolder, "Files", "Data", uuid);
            Directory.CreateDirectory(compatFolder);
            File.Copy(tempFilePath, Path.Combine(compatFolder, "content.rtf"), overwrite: true);

            var result = await rtfConverter.ConvertAsync(tempFolder, uuid, cancellationToken);
            if (result is null)
                throw new UnsupportedFileTypeException(SupportedExtension);

            return result.Html;
        }
        finally
        {
            if (Directory.Exists(tempFolder))
                Directory.Delete(tempFolder, recursive: true);
        }
    }
}

using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using DraftView.Domain.Interfaces.Services;

namespace DraftView.Infrastructure.Parsing;

/// <summary>
/// Parses .docx chapter files using DocumentFormat.OpenXml.
/// Maps Heading1–3 to # / ## / ###, bold runs to **text**, italic runs to *text*.
/// Complex elements (tables, images, footnotes) are stripped without error.
/// </summary>
public class DocxChapterParser : IChapterFileParser
{
    public string SupportedExtension => ".docx";

    /// <summary>
    /// Opens the .docx stream, iterates body paragraphs, and converts each to markdown.
    /// Only top-level paragraphs are processed; table rows and other block-level
    /// containers are skipped.
    /// </summary>
    public Task<string> ParseAsync(Stream content, CancellationToken ct = default)
    {
        using var doc  = WordprocessingDocument.Open(content, false);
        var       body = doc.MainDocumentPart?.Document?.Body;

        if (body is null)
            return Task.FromResult(string.Empty);

        var sb = new System.Text.StringBuilder();

        foreach (var para in body.Elements<Paragraph>())
        {
            var line = ConvertParagraph(para);
            if (line is not null)
            {
                sb.AppendLine(line);
                sb.AppendLine();
            }
        }

        return Task.FromResult(sb.ToString().TrimEnd());
    }

    private static string? ConvertParagraph(Paragraph para)
    {
        var prefix = GetHeadingPrefix(para);
        var text   = BuildRunText(para);

        if (string.IsNullOrWhiteSpace(text))
            return null;

        return prefix is null ? text : $"{prefix} {text}";
    }

    private static string? GetHeadingPrefix(Paragraph para)
    {
        var styleId = para.ParagraphProperties?.ParagraphStyleId?.Val?.Value;
        return styleId switch
        {
            "Heading1" => "#",
            "Heading2" => "##",
            "Heading3" => "###",
            _          => null
        };
    }

    private static string BuildRunText(Paragraph para)
    {
        var sb = new System.Text.StringBuilder();

        foreach (var run in para.Elements<Run>())
        {
            var runText = string.Concat(run.Elements<Text>().Select(t => t.Text));
            if (string.IsNullOrEmpty(runText))
                continue;

            var rpr    = run.RunProperties;
            var bold   = rpr?.Bold is not null;
            var italic = rpr?.Italic is not null;

            if (bold && italic)
                sb.Append($"***{runText}***");
            else if (bold)
                sb.Append($"**{runText}**");
            else if (italic)
                sb.Append($"*{runText}*");
            else
                sb.Append(runText);
        }

        return sb.ToString();
    }
}

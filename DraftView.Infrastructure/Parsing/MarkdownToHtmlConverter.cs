using System.Text;
using System.Text.RegularExpressions;
using DraftView.Domain.Interfaces.Services;

namespace DraftView.Infrastructure.Parsing;

/// <summary>
/// Converts stored markdown content to HTML for reader delivery.
/// Handles: # ## ### headings, **bold**, *italic*, and blank-line-separated paragraphs.
/// </summary>
public sealed partial class MarkdownToHtmlConverter : IMarkdownToHtmlConverter
{
    public string Convert(string markdown)
    {
        if (string.IsNullOrEmpty(markdown))
            return string.Empty;

        var sb     = new StringBuilder();
        var paras  = SplitIntoParagraphs(markdown);

        foreach (var para in paras)
        {
            var trimmed = para.Trim();
            if (string.IsNullOrEmpty(trimmed))
                continue;

            if (trimmed.StartsWith("### ", StringComparison.Ordinal))
                sb.Append("<h3>").Append(InlineFormat(trimmed[4..])).AppendLine("</h3>");
            else if (trimmed.StartsWith("## ", StringComparison.Ordinal))
                sb.Append("<h2>").Append(InlineFormat(trimmed[3..])).AppendLine("</h2>");
            else if (trimmed.StartsWith("# ", StringComparison.Ordinal))
                sb.Append("<h1>").Append(InlineFormat(trimmed[2..])).AppendLine("</h1>");
            else
                sb.Append("<p>").Append(InlineFormat(trimmed)).AppendLine("</p>");
        }

        return sb.ToString().TrimEnd();
    }

    private static IEnumerable<string> SplitIntoParagraphs(string text)
        => BlankLineRegex().Split(text);

    private static string InlineFormat(string text)
    {
        text = BoldRegex().Replace(text, "<strong>$1</strong>");
        text = ItalicRegex().Replace(text, "<em>$1</em>");
        return text;
    }

    [GeneratedRegex(@"\r?\n\s*\r?\n")]
    private static partial Regex BlankLineRegex();

    [GeneratedRegex(@"\*\*(.+?)\*\*")]
    private static partial Regex BoldRegex();

    // Avoid matching ** sequences; only single * on each side
    [GeneratedRegex(@"(?<!\*)\*(?!\*)(.+?)(?<!\*)\*(?!\*)")]
    private static partial Regex ItalicRegex();
}

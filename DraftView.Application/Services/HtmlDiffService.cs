using System.Text.RegularExpressions;
using DraftView.Domain.Diff;
using DraftView.Domain.Enumerations;
using DraftView.Domain.Interfaces.Services;

namespace DraftView.Application.Services;

/// <summary>
/// Computes a paragraph-level diff between two HTML content strings.
/// Uses a Longest Common Subsequence approach comparing stripped paragraph text.
/// </summary>
public class HtmlDiffService : IHtmlDiffService
{
    /// <summary>
    /// Computes a paragraph-level diff between the from and to HTML strings.
    /// Returns a list of ParagraphDiffResult ordered as they appear in the
    /// combined sequence (removed paragraphs from `from`, added paragraphs
    /// from `to`, unchanged paragraphs preserved in position).
    /// </summary>
    public IReadOnlyList<ParagraphDiffResult> Compute(string? from, string? to)
    {
        if (IsNullOrEmpty(from) && IsNullOrEmpty(to))
            return Array.Empty<ParagraphDiffResult>();

        var fromParagraphs = ExtractParagraphs(from ?? string.Empty);
        var toParagraphs = ExtractParagraphs(to ?? string.Empty);

        if (fromParagraphs.Count == 0 && toParagraphs.Count > 0)
            return toParagraphs.Select(p => new ParagraphDiffResult(p.Text, p.Html, DiffResultType.Added)).ToList();

        if (toParagraphs.Count == 0 && fromParagraphs.Count > 0)
            return fromParagraphs.Select(p => new ParagraphDiffResult(p.Text, p.Html, DiffResultType.Removed)).ToList();

        return ComputeDiff(fromParagraphs, toParagraphs);
    }

    private static List<ParagraphDiffResult> ComputeDiff(
        List<(string Text, string Html)> from,
        List<(string Text, string Html)> to)
    {
        var lcs = ComputeLcs(from.Select(p => p.Text).ToList(), to.Select(p => p.Text).ToList());
        var result = new List<ParagraphDiffResult>();

        int fromIndex = 0;
        int toIndex = 0;
        int lcsIndex = 0;

        while (fromIndex < from.Count || toIndex < to.Count)
        {
            if (lcsIndex < lcs.Count)
            {
                var lcsText = lcs[lcsIndex];

                while (fromIndex < from.Count && from[fromIndex].Text != lcsText)
                {
                    result.Add(new ParagraphDiffResult(from[fromIndex].Text, from[fromIndex].Html, DiffResultType.Removed));
                    fromIndex++;
                }

                while (toIndex < to.Count && to[toIndex].Text != lcsText)
                {
                    result.Add(new ParagraphDiffResult(to[toIndex].Text, to[toIndex].Html, DiffResultType.Added));
                    toIndex++;
                }

                if (fromIndex < from.Count && toIndex < to.Count)
                {
                    result.Add(new ParagraphDiffResult(to[toIndex].Text, to[toIndex].Html, DiffResultType.Unchanged));
                    fromIndex++;
                    toIndex++;
                    lcsIndex++;
                }
            }
            else
            {
                while (fromIndex < from.Count)
                {
                    result.Add(new ParagraphDiffResult(from[fromIndex].Text, from[fromIndex].Html, DiffResultType.Removed));
                    fromIndex++;
                }

                while (toIndex < to.Count)
                {
                    result.Add(new ParagraphDiffResult(to[toIndex].Text, to[toIndex].Html, DiffResultType.Added));
                    toIndex++;
                }
            }
        }

        return result;
    }

    private static List<string> ComputeLcs(List<string> from, List<string> to)
    {
        int m = from.Count;
        int n = to.Count;
        var dp = new int[m + 1, n + 1];

        for (int i = 1; i <= m; i++)
        {
            for (int j = 1; j <= n; j++)
            {
                if (from[i - 1] == to[j - 1])
                    dp[i, j] = dp[i - 1, j - 1] + 1;
                else
                    dp[i, j] = Math.Max(dp[i - 1, j], dp[i, j - 1]);
            }
        }

        var lcs = new List<string>();
        int fi = m, ti = n;

        while (fi > 0 && ti > 0)
        {
            if (from[fi - 1] == to[ti - 1])
            {
                lcs.Insert(0, from[fi - 1]);
                fi--;
                ti--;
            }
            else if (dp[fi - 1, ti] > dp[fi, ti - 1])
                fi--;
            else
                ti--;
        }

        return lcs;
    }

    private static List<(string Text, string Html)> ExtractParagraphs(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return new List<(string, string)>();

        var paragraphs = new List<(string Text, string Html)>();
        var pattern = @"<p[^>]*>(.*?)</p>";
        var matches = Regex.Matches(html, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);

        if (matches.Count == 0)
        {
            var stripped = StripTags(html);
            if (!string.IsNullOrWhiteSpace(stripped))
                paragraphs.Add((stripped, html));
        }
        else
        {
            foreach (Match match in matches)
            {
                var innerHtml = match.Groups[1].Value;
                var stripped = StripTags(innerHtml);
                if (!string.IsNullOrWhiteSpace(stripped))
                    paragraphs.Add((stripped, match.Value));
            }
        }

        return paragraphs;
    }

    private static string StripTags(string html)
        => Regex.Replace(html, "<[^>]+>", string.Empty).Trim();

    private static bool IsNullOrEmpty(string? value)
        => string.IsNullOrWhiteSpace(value);
}

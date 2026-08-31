using System.Text;
using System.Text.RegularExpressions;
using DraftView.Domain.Diff;
using DraftView.Domain.Enumerations;
using DraftView.Domain.Interfaces.Services;

namespace DraftView.Application.Services;

/// <summary>
/// Computes a word-level diff between two HTML content strings.
/// Paragraphs are first matched using LCS. Matched pairs with word-level changes
/// produce a Modified result with inline del/ins spans. Purely added or removed
/// paragraphs retain their Added/Removed type. All results carry word count data
/// for use by IChangeClassificationService.
/// </summary>
public class HtmlDiffService : IHtmlDiffService
{
    /// <summary>
    /// Computes a word-level diff between the from and to HTML strings.
    /// Returns an ordered list of ParagraphDiffResult. Modified paragraphs carry
    /// inline del/ins HTML spans showing removed and inserted words.
    /// </summary>
    public IReadOnlyList<ParagraphDiffResult> Compute(string? from, string? to)
    {
        if (IsNullOrEmpty(from) && IsNullOrEmpty(to))
            return Array.Empty<ParagraphDiffResult>();

        var fromParagraphs = ExtractParagraphs(from ?? string.Empty);
        var toParagraphs   = ExtractParagraphs(to   ?? string.Empty);

        if (fromParagraphs.Count == 0 && toParagraphs.Count > 0)
            return toParagraphs.Select(p => AddedParagraph(p)).ToList();

        if (toParagraphs.Count == 0 && fromParagraphs.Count > 0)
            return fromParagraphs.Select(p => RemovedParagraph(p)).ToList();

        return ComputeDiff(fromParagraphs, toParagraphs);
    }

    private static IReadOnlyList<ParagraphDiffResult> ComputeDiff(
        List<(string Text, string Html)> from,
        List<(string Text, string Html)> to)
    {
        var lcs    = ComputeParagraphLcs(from.Select(p => p.Text).ToList(), to.Select(p => p.Text).ToList());
        var rawDiff = BuildRawDiff(from, to, lcs);
        return MergeModifiedParagraphs(rawDiff);
    }

    /// <summary>
    /// Walks the paragraph LCS to produce an initial Removed/Added/Unchanged list,
    /// then merges consecutive equal-count Removed+Added pairs into Modified.
    /// </summary>
    private static IReadOnlyList<ParagraphDiffResult> MergeModifiedParagraphs(
        List<(string Text, string Html, DiffResultType Type)> raw)
    {
        var result = new List<ParagraphDiffResult>();
        int i = 0;

        while (i < raw.Count)
        {
            if (raw[i].Type == DiffResultType.Unchanged)
            {
                var p = raw[i++];
                result.Add(UnchangedParagraph((p.Text, p.Html)));
                continue;
            }

            var removedBatch = new List<(string Text, string Html)>();
            while (i < raw.Count && raw[i].Type == DiffResultType.Removed)
                removedBatch.Add((raw[i].Text, raw[i++].Html));

            var addedBatch = new List<(string Text, string Html)>();
            while (i < raw.Count && raw[i].Type == DiffResultType.Added)
                addedBatch.Add((raw[i].Text, raw[i++].Html));

            if (removedBatch.Count > 0 && addedBatch.Count > 0
                && removedBatch.Count == addedBatch.Count)
            {
                for (int k = 0; k < removedBatch.Count; k++)
                    result.Add(ApplyWordLevelDiff(removedBatch[k].Text, addedBatch[k].Text));
            }
            else
            {
                foreach (var p in removedBatch)
                    result.Add(RemovedParagraph(p));
                foreach (var p in addedBatch)
                    result.Add(AddedParagraph(p));
            }
        }

        return result;
    }

    private static List<(string Text, string Html, DiffResultType Type)> BuildRawDiff(
        List<(string Text, string Html)> from,
        List<(string Text, string Html)> to,
        List<string> lcs)
    {
        var result   = new List<(string, string, DiffResultType)>();
        int fromIndex = 0, toIndex = 0, lcsIndex = 0;

        while (fromIndex < from.Count || toIndex < to.Count)
        {
            if (lcsIndex < lcs.Count)
            {
                var lcsText = lcs[lcsIndex];

                while (fromIndex < from.Count && from[fromIndex].Text != lcsText)
                    result.Add((from[fromIndex].Text, from[fromIndex++].Html, DiffResultType.Removed));

                while (toIndex < to.Count && to[toIndex].Text != lcsText)
                    result.Add((to[toIndex].Text, to[toIndex++].Html, DiffResultType.Added));

                if (fromIndex < from.Count && toIndex < to.Count)
                {
                    result.Add((to[toIndex].Text, to[toIndex].Html, DiffResultType.Unchanged));
                    fromIndex++; toIndex++; lcsIndex++;
                }
            }
            else
            {
                while (fromIndex < from.Count)
                    result.Add((from[fromIndex].Text, from[fromIndex++].Html, DiffResultType.Removed));

                while (toIndex < to.Count)
                    result.Add((to[toIndex].Text, to[toIndex++].Html, DiffResultType.Added));
            }
        }

        return result;
    }

    /// <summary>
    /// Applies word-level LCS diff between two paragraph texts.
    /// Produces a Modified ParagraphDiffResult with del/ins spans in the HTML.
    /// </summary>
    private static ParagraphDiffResult ApplyWordLevelDiff(string fromText, string toText)
    {
        var fromWords = Tokenize(fromText);
        var toWords   = Tokenize(toText);
        var lcs       = ComputeWordLcs(fromWords, toWords);

        var sb = new StringBuilder("<p>");
        int fi = 0, ti = 0, li = 0;
        int wordsAdded = 0, wordsRemoved = 0;

        while (fi < fromWords.Length || ti < toWords.Length)
        {
            if (li < lcs.Count)
            {
                var lcsWord = lcs[li];

                while (fi < fromWords.Length && fromWords[fi] != lcsWord)
                {
                    sb.Append("<del>").Append(HtmlEncode(fromWords[fi++])).Append("</del> ");
                    wordsRemoved++;
                }

                while (ti < toWords.Length && toWords[ti] != lcsWord)
                {
                    sb.Append("<ins>").Append(HtmlEncode(toWords[ti++])).Append("</ins> ");
                    wordsAdded++;
                }

                if (fi < fromWords.Length && ti < toWords.Length)
                {
                    sb.Append(HtmlEncode(fromWords[fi++])).Append(' ');
                    ti++; li++;
                }
            }
            else
            {
                while (fi < fromWords.Length)
                {
                    sb.Append("<del>").Append(HtmlEncode(fromWords[fi++])).Append("</del> ");
                    wordsRemoved++;
                }

                while (ti < toWords.Length)
                {
                    sb.Append("<ins>").Append(HtmlEncode(toWords[ti++])).Append("</ins> ");
                    wordsAdded++;
                }
            }
        }

        var diffHtml = sb.ToString().TrimEnd() + "</p>";
        return new ParagraphDiffResult(toText, diffHtml, DiffResultType.Modified,
            wordsAdded: wordsAdded, wordsRemoved: wordsRemoved, totalWords: toWords.Length);
    }

    private static ParagraphDiffResult UnchangedParagraph((string Text, string Html) p)
    {
        var wordCount = CountWords(p.Text);
        return new ParagraphDiffResult(p.Text, p.Html, DiffResultType.Unchanged,
            wordsAdded: 0, wordsRemoved: 0, totalWords: wordCount);
    }

    private static ParagraphDiffResult AddedParagraph((string Text, string Html) p)
    {
        var wordCount = CountWords(p.Text);
        return new ParagraphDiffResult(p.Text, p.Html, DiffResultType.Added,
            wordsAdded: wordCount, wordsRemoved: 0, totalWords: wordCount);
    }

    private static ParagraphDiffResult RemovedParagraph((string Text, string Html) p)
    {
        var wordCount = CountWords(p.Text);
        return new ParagraphDiffResult(p.Text, p.Html, DiffResultType.Removed,
            wordsAdded: 0, wordsRemoved: wordCount, totalWords: 0);
    }

    private static List<string> ComputeParagraphLcs(List<string> from, List<string> to)
    {
        int m = from.Count, n = to.Count;
        var dp = new int[m + 1, n + 1];

        for (int i = 1; i <= m; i++)
            for (int j = 1; j <= n; j++)
                dp[i, j] = from[i - 1] == to[j - 1]
                    ? dp[i - 1, j - 1] + 1
                    : Math.Max(dp[i - 1, j], dp[i, j - 1]);

        var lcs = new List<string>();
        int fi = m, ti = n;

        while (fi > 0 && ti > 0)
        {
            if (from[fi - 1] == to[ti - 1]) { lcs.Insert(0, from[fi - 1]); fi--; ti--; }
            else if (dp[fi - 1, ti] > dp[fi, ti - 1]) fi--;
            else ti--;
        }

        return lcs;
    }

    private static List<string> ComputeWordLcs(string[] from, string[] to)
    {
        int m = from.Length, n = to.Length;
        var dp = new int[m + 1, n + 1];

        for (int i = 1; i <= m; i++)
            for (int j = 1; j <= n; j++)
                dp[i, j] = from[i - 1] == to[j - 1]
                    ? dp[i - 1, j - 1] + 1
                    : Math.Max(dp[i - 1, j], dp[i, j - 1]);

        var lcs = new List<string>();
        int fi = m, ti = n;

        while (fi > 0 && ti > 0)
        {
            if (from[fi - 1] == to[ti - 1]) { lcs.Insert(0, from[fi - 1]); fi--; ti--; }
            else if (dp[fi - 1, ti] > dp[fi, ti - 1]) fi--;
            else ti--;
        }

        return lcs;
    }

    private static List<(string Text, string Html)> ExtractParagraphs(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return new List<(string, string)>();

        var paragraphs = new List<(string Text, string Html)>();
        var matches    = Regex.Matches(html, @"<p[^>]*>(.*?)</p>",
                             RegexOptions.IgnoreCase | RegexOptions.Singleline);

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
                var stripped = StripTags(match.Groups[1].Value);
                if (!string.IsNullOrWhiteSpace(stripped))
                    paragraphs.Add((stripped, match.Value));
            }
        }

        return paragraphs;
    }

    private static string[] Tokenize(string text)
        => text.Split(' ', StringSplitOptions.RemoveEmptyEntries);

    private static int CountWords(string text)
        => string.IsNullOrWhiteSpace(text) ? 0 : Tokenize(text).Length;

    private static string StripTags(string html)
        => Regex.Replace(html, "<[^>]+>", string.Empty).Trim();

    private static string HtmlEncode(string word)
        => word.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

    private static bool IsNullOrEmpty(string? value)
        => string.IsNullOrWhiteSpace(value);
}

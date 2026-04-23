namespace DraftView.Application.Services;

/// <summary>
/// Centralizes deterministic confidence scoring and acceptance thresholds for passage anchor matching.
/// </summary>
public static class PassageAnchorConfidence
{
    public const int Exact = 100;
    public const int Context = 80;
    public const int FuzzyThreshold = 65;

    /// <summary>
    /// Returns a normalized 0-100 confidence score from two candidate texts using edit distance.
    /// </summary>
    public static int FromEditDistance(string left, string right)
    {
        if (left.Length == 0 && right.Length == 0)
            return Exact;

        var distance = ComputeLevenshteinDistance(left, right);
        var maxLength = Math.Max(left.Length, right.Length);
        var rawScore = 100.0 * (1.0 - ((double)distance / maxLength));
        return Normalize((int)Math.Round(rawScore));
    }

    /// <summary>
    /// Returns true when the fuzzy score is high enough to accept as a match.
    /// </summary>
    public static bool IsFuzzyMatchAcceptable(int score) => score >= FuzzyThreshold;

    /// <summary>
    /// Clamps any score to the inclusive 0-100 range.
    /// </summary>
    public static int Normalize(int score) => Math.Clamp(score, 0, Exact);

    /// <summary>
    /// Computes the deterministic Levenshtein distance used by the confidence score.
    /// </summary>
    private static int ComputeLevenshteinDistance(string left, string right)
    {
        var rows = left.Length + 1;
        var cols = right.Length + 1;
        var matrix = new int[rows, cols];

        for (var i = 0; i < rows; i++)
            matrix[i, 0] = i;

        for (var j = 0; j < cols; j++)
            matrix[0, j] = j;

        for (var i = 1; i < rows; i++)
        {
            for (var j = 1; j < cols; j++)
            {
                var cost = left[i - 1] == right[j - 1] ? 0 : 1;
                matrix[i, j] = Math.Min(
                    Math.Min(matrix[i - 1, j] + 1, matrix[i, j - 1] + 1),
                    matrix[i - 1, j - 1] + cost);
            }
        }

        return matrix[left.Length, right.Length];
    }
}

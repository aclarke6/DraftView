using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace DraftView.Web.Tests;

public class GoverningEmailExposureTests
{
    /// <summary>
    /// LONG-RUNNING GOVERNING REGRESSION TEST.
    /// 
    /// Enforces that no non-whitelisted Razor view contains any email display bindings.
    /// 
    /// This test MUST remain RED until Phase 2 (Web Surface Cleanup) is implemented.
    /// 
    /// Failure indicates a GDPR-critical data exposure risk.
    /// </summary>
    [Fact]
    public void Governing_SourceLevel_NoEmailBindingsInNonWhitelistedViews_MUST_FAIL_UNTIL_PHASE2()
    {
        // Arrange
        var solutionRoot = GetSolutionRoot();
        var viewsPath = Path.Combine(solutionRoot, "DraftView.Web", "Views");

        var whitelist = new[]
        {
            "Account\\Settings.cshtml"
        };

        var forbiddenPatterns = new[]
        {
            new Regex(@"@\s*Model\.Email\b", RegexOptions.IgnoreCase),
            new Regex(@"@\s*[A-Za-z_][A-Za-z0-9_]*\.Email\b", RegexOptions.IgnoreCase),
            new Regex(@"DisplayFor\s*\([^)]*Email\b", RegexOptions.IgnoreCase),
            new Regex(@"mailto\s*:\s*[^""'\s>]+", RegexOptions.IgnoreCase),

            // Identity-based exposure (CRITICAL GAP)
            new Regex(@"@User\.Identity\.Name\b", RegexOptions.IgnoreCase),
            new Regex(@"User\.Identity\.Name", RegexOptions.IgnoreCase)
        };

        var violations = Directory
            .GetFiles(viewsPath, "*.cshtml", SearchOption.AllDirectories)
            .Where(file => !IsWhitelisted(file, whitelist))
            .SelectMany(file =>
            {
                var lines = File.ReadAllLines(file);

                return lines
                    .Select((line, index) => new { line, index })
                    .Where(x => forbiddenPatterns.Any(p => p.IsMatch(x.line)))
                    .Select(x => $"{GetRelativePath(file, viewsPath)} (line {x.index + 1}): {x.line.Trim()}");
            })
            .ToList();

        // Assert
        Assert.True(!violations.Any(),
            "Email exposure detected in non-whitelisted views:\n" +
            string.Join(Environment.NewLine, violations));
    }

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private static bool IsWhitelisted(string filePath, string[] whitelist)
    {
        return whitelist.Any(w => filePath.EndsWith(w, StringComparison.OrdinalIgnoreCase));
    }

    private static string GetSolutionRoot()
    {
        var dir = Directory.GetCurrentDirectory();

        while (dir != null &&
               !Directory.GetFiles(dir, "*.sln").Any() &&
               !Directory.GetFiles(dir, "*.slnx").Any())
        {
            dir = Directory.GetParent(dir)?.FullName;
        }

        if (dir == null)
            throw new Exception("Solution root not found (.sln or .slnx).");

        return dir;
    }

    private static string GetRelativePath(string fullPath, string root)
    {
        return fullPath.Replace(root, "").TrimStart(Path.DirectorySeparatorChar);
    }
}
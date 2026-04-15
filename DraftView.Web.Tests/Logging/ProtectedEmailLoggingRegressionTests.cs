using System.Text.RegularExpressions;
using Xunit;

namespace DraftView.Web.Tests.Logging;

public class ProtectedEmailLoggingRegressionTests
{
    [Theory]
    [InlineData("DraftView.Web/Controllers/AccountController.cs")]
    [InlineData("DraftView.Web/Data/DatabaseSeeder.cs")]
    public void ProtectedFlows_DoNotLogPlaintextEmailPlaceholders(string relativePath)
    {
        var solutionRoot = GetSolutionRoot();
        var fullPath = Path.Combine(solutionRoot, relativePath);
        var content = File.ReadAllText(fullPath);

        var forbiddenPatterns = new[]
        {
            new Regex(@"Log(?:Information|Warning|Error)\([^;\r\n]*\{Email\}", RegexOptions.IgnoreCase),
            new Regex(@"Log(?:Information|Warning|Error)\([^;\r\n]*plaintext email", RegexOptions.IgnoreCase)
        };

        Assert.DoesNotContain(
            forbiddenPatterns,
            pattern => pattern.IsMatch(content));
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
            throw new InvalidOperationException("Solution root not found (.sln or .slnx).");

        return dir;
    }
}

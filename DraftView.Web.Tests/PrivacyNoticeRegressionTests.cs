using Xunit;

namespace DraftView.Web.Tests;

public class PrivacyNoticeRegressionTests
{
    [Theory]
    [InlineData("DraftView.Web/Views/Shared/_Layout.cshtml", "Privacy Notice")]
    [InlineData("DraftView.Web/Views/Home/Privacy.cshtml", "service-related communication")]
    [InlineData("DraftView.Web/Views/Home/Privacy.cshtml", "does not use account email addresses for marketing")]
    [InlineData("DraftView.Web/Views/Home/Privacy.cshtml", "does not share account email addresses with third parties for unrelated purposes")]
    [InlineData("DraftView.Web/Views/Home/Privacy.cshtml", "UK GDPR")]
    [InlineData("DraftView.Web/Views/Home/Privacy.cshtml", "Data Protection Act 2018")]
    public void PrivacyNotice_ContentAndVisibilityRemainPresent(string relativePath, string expectedContent)
    {
        var solutionRoot = GetSolutionRoot();
        var fullPath = Path.Combine(solutionRoot, relativePath);
        var content = File.ReadAllText(fullPath);

        Assert.Contains(expectedContent, content, StringComparison.OrdinalIgnoreCase);
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

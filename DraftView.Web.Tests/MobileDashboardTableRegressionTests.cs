using Xunit;

namespace DraftView.Web.Tests;

/// <summary>
/// File-based regressions for the author dashboard markup and styles.
/// Covers: replacement of the flat published chapter table with the hierarchical
/// reader-progress component and its supporting CSS hooks.
/// Excludes: runtime Razor rendering and controller data mapping.
/// </summary>
public class MobileDashboardTableRegressionTests
{
    [Fact]
    public void DashboardView_ReplacesFlatPublishedChapterTableWithHierarchicalReaderProgress()
    {
        var solutionRoot = GetSolutionRoot();
        var dashboardCssPath = Path.Combine(solutionRoot, "DraftView.Web", "wwwroot", "css", "DraftView.Dashboard.css");
        var dashboardViewPath = Path.Combine(solutionRoot, "DraftView.Web", "Views", "Author", "Dashboard.cshtml");
        var chapterPartialPath = Path.Combine(solutionRoot, "DraftView.Web", "Views", "Author", "_DashboardChapterProgress.cshtml");

        var dashboardCss = File.ReadAllText(dashboardCssPath);
        var dashboardView = File.ReadAllText(dashboardViewPath);
        var chapterPartial = File.ReadAllText(chapterPartialPath);

        Assert.Contains(".dashboard-progress__summary {", dashboardCss, StringComparison.Ordinal);
        Assert.Contains(".dashboard-progress__reader {", dashboardCss, StringComparison.Ordinal);
        Assert.Contains(".dashboard-progress__status--not-viewed {", dashboardCss, StringComparison.Ordinal);
        Assert.Contains("class=\"dashboard-table__actions projects-table__col-actions\"", dashboardView, StringComparison.Ordinal);
        Assert.Contains("class=\"projects-table__col-sync\"", dashboardView, StringComparison.Ordinal);
        Assert.Contains("class=\"projects-table__col-reader-active\"", dashboardView, StringComparison.Ordinal);
        Assert.Contains("Reader Progress", dashboardView, StringComparison.Ordinal);
        Assert.Contains("class=\"dashboard-progress__group\"", dashboardView, StringComparison.Ordinal);
        Assert.Contains("Html.PartialAsync(\"_DashboardChapterProgress\", chapter)", dashboardView, StringComparison.Ordinal);
        Assert.Contains("Rotate to landscape to view additional project details.", dashboardView, StringComparison.Ordinal);
        Assert.Contains("Expand acts, chapters, and readers without leaving the dashboard.", dashboardView, StringComparison.Ordinal);
        Assert.DoesNotContain("class=\"published-table__col-published\"", dashboardView, StringComparison.Ordinal);
        Assert.DoesNotContain("class=\"published-table__col-changed\"", dashboardView, StringComparison.Ordinal);
        Assert.Contains("Not viewed yet", chapterPartial, StringComparison.Ordinal);
        Assert.Contains("Latest comment on", chapterPartial, StringComparison.Ordinal);
        Assert.Contains("dashboard-progress__summary-toggle", chapterPartial, StringComparison.Ordinal);
        Assert.Contains("class=\"dashboard-progress__chapter-links\"", chapterPartial, StringComparison.Ordinal);
        Assert.Contains("Url.Action(\"Read\", \"Reader\", new { id = Model.Chapter.Id })", chapterPartial, StringComparison.Ordinal);
        Assert.Contains(">Reader page</a>", chapterPartial, StringComparison.Ordinal);
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

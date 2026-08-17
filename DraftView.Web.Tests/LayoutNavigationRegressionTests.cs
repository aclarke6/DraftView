using Xunit;

namespace DraftView.Web.Tests;

public class LayoutNavigationRegressionTests
{
    [Fact]
    public void SharedLayout_MobileNavigationToggle_UsesMenuIconInsteadOfQuestionMarkPlaceholder()
    {
        var solutionRoot = GetSolutionRoot();
        var iconPath = Path.Combine(solutionRoot, "DraftView.Web", "Infrastructure", "Icons.cs");
        var layoutPath = Path.Combine(solutionRoot, "DraftView.Web", "Views", "Shared", "_Layout.cshtml");

        var iconSource = File.ReadAllText(iconPath);
        var layoutSource = File.ReadAllText(layoutPath);

        Assert.Contains("public static IHtmlContent Menu()", iconSource, StringComparison.Ordinal);
        Assert.Contains("@Icons.Menu()", layoutSource, StringComparison.Ordinal);
        Assert.DoesNotContain(">\r\n                    ?\r\n                </button>", layoutSource, StringComparison.Ordinal);
        Assert.DoesNotContain(">\n                    ?\n                </button>", layoutSource, StringComparison.Ordinal);
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

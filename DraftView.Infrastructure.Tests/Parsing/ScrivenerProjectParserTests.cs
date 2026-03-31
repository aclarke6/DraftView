using DraftView.Infrastructure.Parsing;
using DraftView.Domain.Interfaces.Services;

namespace DraftView.Infrastructure.Tests.Parsing;

public class ScrivenerProjectParserTests
{
    private readonly string _testDataPath;

    public ScrivenerProjectParserTests()
    {
        _testDataPath = Path.Combine(
            AppContext.BaseDirectory, "TestData");
    }

    private string MinimalScrivx => Path.Combine(_testDataPath, "minimal.scrivx");

    // ---------------------------------------------------------------------------
    // ParseStatusMap
    // ---------------------------------------------------------------------------

    [Fact]
    public void ParseStatusMap_ReturnsCorrectMappings()
    {
        var parser = new ScrivenerProjectParser();

        var result = parser.Parse(MinimalScrivx);

        Assert.Equal("First Draft", result.StatusMap["2"]);
        Assert.Equal("To Do", result.StatusMap["1"]);
        Assert.Equal("No Status", result.StatusMap["-1"]);
    }

    // ---------------------------------------------------------------------------
    // Parse - DraftFolder discovery
    // ---------------------------------------------------------------------------

    [Fact]
    public void Parse_FindsDraftFolderAsRoot()
    {
        var parser = new ScrivenerProjectParser();

        var result = parser.Parse(MinimalScrivx);

        Assert.NotNull(result.ManuscriptRoot);
        Assert.Equal("Manuscript", result.ManuscriptRoot!.Title);
        Assert.Equal("DRAFT-001", result.ManuscriptRoot.Uuid);
    }

    // ---------------------------------------------------------------------------
    // Parse - tree structure
    // ---------------------------------------------------------------------------

    [Fact]
    public void Parse_BuildsCorrectTreeStructure()
    {
        var parser = new ScrivenerProjectParser();

        var result = parser.Parse(MinimalScrivx);

        var root = result.ManuscriptRoot!;
        Assert.Single(root.Children); // Book 1

        var book = root.Children[0];
        Assert.Equal("Book 1", book.Title);
        Assert.Single(book.Children); // Chapter 1

        var chapter = book.Children[0];
        Assert.Equal("Chapter 1", chapter.Title);
        Assert.Equal(2, chapter.Children.Count); // Scene 1, Scene 2
    }

    [Fact]
    public void Parse_SetsNodeTypesCorrectly()
    {
        var parser = new ScrivenerProjectParser();

        var result = parser.Parse(MinimalScrivx);

        var book = result.ManuscriptRoot!.Children[0];
        Assert.Equal(ParsedNodeType.Folder, book.NodeType);

        var scene = result.ManuscriptRoot.Children[0].Children[0].Children[0];
        Assert.Equal(ParsedNodeType.Document, scene.NodeType);
    }

    [Fact]
    public void Parse_SetsSortOrderByPosition()
    {
        var parser = new ScrivenerProjectParser();

        var result = parser.Parse(MinimalScrivx);

        var chapter = result.ManuscriptRoot!.Children[0].Children[0];
        Assert.Equal(0, chapter.Children[0].SortOrder);
        Assert.Equal(1, chapter.Children[1].SortOrder);
    }

    // ---------------------------------------------------------------------------
    // Parse - status resolution
    // ---------------------------------------------------------------------------

    [Fact]
    public void Parse_ResolvesStatusIdToStatusName()
    {
        var parser = new ScrivenerProjectParser();

        var result = parser.Parse(MinimalScrivx);

        var scene1 = result.ManuscriptRoot!.Children[0].Children[0].Children[0];
        Assert.Equal("First Draft", scene1.ScrivenerStatus);

        var scene2 = result.ManuscriptRoot.Children[0].Children[0].Children[1];
        Assert.Equal("To Do", scene2.ScrivenerStatus);
    }

    [Fact]
    public void Parse_SetsNullStatusWhenNoStatusId()
    {
        var parser = new ScrivenerProjectParser();

        var result = parser.Parse(MinimalScrivx);

        // Book 1 folder has no StatusID
        var book = result.ManuscriptRoot!.Children[0];
        Assert.Null(book.ScrivenerStatus);
    }

    // ---------------------------------------------------------------------------
    // Parse - exclusions
    // ---------------------------------------------------------------------------

    [Fact]
    public void Parse_ExcludesTrashFolder()
    {
        var parser = new ScrivenerProjectParser();

        var result = parser.Parse(MinimalScrivx);

        var allUuids = GetAllUuids(result.ManuscriptRoot!);
        Assert.DoesNotContain("TRASH-001", allUuids);
        Assert.DoesNotContain("DELETED-001", allUuids);
    }

    [Fact]
    public void Parse_ExcludesResearchFolder()
    {
        var parser = new ScrivenerProjectParser();

        var result = parser.Parse(MinimalScrivx);

        var allUuids = GetAllUuids(result.ManuscriptRoot!);
        Assert.DoesNotContain("RESEARCH-001", allUuids);
    }

    [Fact]
    public void Parse_ExcludesNonDraftTopLevelFolders()
    {
        var parser = new ScrivenerProjectParser();

        var result = parser.Parse(MinimalScrivx);

        var allUuids = GetAllUuids(result.ManuscriptRoot!);
        Assert.DoesNotContain("CHARS-001", allUuids);
    }

    // ---------------------------------------------------------------------------
    // Parse - UUIDs
    // ---------------------------------------------------------------------------

    [Fact]
    public void Parse_SetsUuidsCorrectly()
    {
        var parser = new ScrivenerProjectParser();

        var result = parser.Parse(MinimalScrivx);

        var scene1 = result.ManuscriptRoot!.Children[0].Children[0].Children[0];
        Assert.Equal("SCEN-001", scene1.Uuid);
    }

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private static IEnumerable<string> GetAllUuids(ParsedBinderNode node)
    {
        yield return node.Uuid;
        foreach (var child in node.Children)
            foreach (var uuid in GetAllUuids(child))
                yield return uuid;
    }
}

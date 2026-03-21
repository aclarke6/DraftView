namespace ScrivenerSync.Domain.Interfaces.Services;

public enum ParsedNodeType
{
    Folder,
    Document
}

public sealed class ParsedBinderNode
{
    public string Uuid { get; init; } = default!;
    public string Title { get; init; } = default!;
    public ParsedNodeType NodeType { get; init; }
    public string? ScrivenerStatus { get; init; }
    public int SortOrder { get; init; }
    public List<ParsedBinderNode> Children { get; init; } = new();
}

public sealed class ParsedProject
{
    public ParsedBinderNode? ManuscriptRoot { get; init; }
    public Dictionary<string, string> StatusMap { get; init; } = new();
}

public interface IScrivenerProjectParser
{
    ParsedProject Parse(string scrivxPath);
}

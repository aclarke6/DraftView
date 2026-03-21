using System.Xml.Linq;
using ScrivenerSync.Domain.Interfaces.Services;

namespace ScrivenerSync.Infrastructure.Parsing;

public class ScrivenerProjectParser : IScrivenerProjectParser
{
    private static readonly HashSet<string> SkippedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "TrashFolder", "ResearchFolder", "Image", "PDF", "Media",
        "Snapshot", "WebArchive"
    };

    private static readonly HashSet<string> FolderTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Folder", "DraftFolder"
    };

    public ParsedProject Parse(string scrivxPath)
    {
        var doc = XDocument.Load(scrivxPath);
        var root = doc.Root ?? throw new InvalidOperationException("Invalid .scrivx file: no root element.");

        var statusMap = ParseStatusMap(root);
        var manuscriptRoot = FindAndParseManuscript(root, statusMap);

        return new ParsedProject
        {
            ManuscriptRoot = manuscriptRoot,
            StatusMap      = statusMap
        };
    }

    private static Dictionary<string, string> ParseStatusMap(XElement root)
    {
        var map = new Dictionary<string, string>();

        var statusItems = root
            .Element("StatusSettings")
            ?.Element("StatusItems")
            ?.Elements("Status")
            ?? Enumerable.Empty<XElement>();

        foreach (var status in statusItems)
        {
            var id    = status.Attribute("ID")?.Value;
            var label = status.Value.Trim();
            if (id is not null)
                map[id] = label;
        }

        return map;
    }

    private static ParsedBinderNode? FindAndParseManuscript(
        XElement root,
        Dictionary<string, string> statusMap)
    {
        var binder = root.Element("Binder");
        if (binder is null) return null;

        var draftFolder = binder
            .Elements("BinderItem")
            .FirstOrDefault(e => e.Attribute("Type")?.Value == "DraftFolder");

        if (draftFolder is null) return null;

        return ParseNode(draftFolder, statusMap, sortOrder: 0);
    }

    private static ParsedBinderNode ParseNode(
        XElement element,
        Dictionary<string, string> statusMap,
        int sortOrder)
    {
        var uuid  = element.Attribute("UUID")?.Value ?? string.Empty;
        var type  = element.Attribute("Type")?.Value ?? string.Empty;
        var title = element.Element("Title")?.Value?.Trim() ?? string.Empty;

        var nodeType = FolderTypes.Contains(type)
            ? ParsedNodeType.Folder
            : ParsedNodeType.Document;

        var statusId       = element.Element("MetaData")?.Element("StatusID")?.Value;
        var statusResolved = statusId is not null && statusMap.TryGetValue(statusId, out var s) ? s : null;

        var children = ParseChildren(element, statusMap);

        return new ParsedBinderNode
        {
            Uuid            = uuid,
            Title           = title,
            NodeType        = nodeType,
            ScrivenerStatus = statusResolved,
            SortOrder       = sortOrder,
            Children        = children
        };
    }

    private static List<ParsedBinderNode> ParseChildren(
        XElement element,
        Dictionary<string, string> statusMap)
    {
        var childrenElement = element.Element("Children");
        if (childrenElement is null) return new List<ParsedBinderNode>();

        var result    = new List<ParsedBinderNode>();
        var sortOrder = 0;

        foreach (var child in childrenElement.Elements("BinderItem"))
        {
            var type = child.Attribute("Type")?.Value ?? string.Empty;
            if (SkippedTypes.Contains(type))
                continue;

            result.Add(ParseNode(child, statusMap, sortOrder));
            sortOrder++;
        }

        return result;
    }
}

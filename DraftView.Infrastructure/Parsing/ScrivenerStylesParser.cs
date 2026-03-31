using System.Xml.Linq;

namespace DraftView.Infrastructure.Parsing;

/// <summary>
/// Parsed representation of a single Scrivener named style.
/// </summary>
public record ScrivenerStyle(
    int    Id,
    string Name,
    string Type,         // "character" or "paragraph"
    string CssClassName  // e.g. "scr-style-dialogue"
);

/// <summary>
/// Reads Settings/styles.xml from a Scrivener vault and returns
/// a map of style index -> ScrivenerStyle.
/// Returns an empty dictionary if the file does not exist (vault has no
/// custom styles defined).
/// </summary>
public class ScrivenerStylesParser
{
    public static Dictionary<int, ScrivenerStyle> Parse(string scrivFolderPath)
    {
        var path = Path.Combine(scrivFolderPath, "Settings", "styles.xml");

        if (!File.Exists(path))
            return new Dictionary<int, ScrivenerStyle>();

        var doc    = XDocument.Load(path);
        var result = new Dictionary<int, ScrivenerStyle>();

        // Scrivener styles.xml root element varies by version; we search
        // all descendants for <Style> elements.
        foreach (var el in doc.Descendants("Style"))
        {
            var idAttr   = el.Attribute("ID")   ?? el.Attribute("Id");
            var nameAttr = el.Attribute("Name");
            var typeAttr = el.Attribute("Type");

            if (idAttr is null || nameAttr is null)
                continue;

            if (!int.TryParse(idAttr.Value, out var id))
                continue;

            var name = nameAttr.Value.Trim();
            var type = typeAttr?.Value?.Trim() ?? "character";

            // Produce a safe CSS class name: lowercase, spaces -> hyphens,
            // strip non-alphanumeric except hyphens.
            var cssName = "scr-style-" + MakeCssIdentifier(name);

            result[id] = new ScrivenerStyle(id, name, type, cssName);
        }

        return result;
    }

    private static string MakeCssIdentifier(string name)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var ch in name.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(ch))
                sb.Append(ch);
            else if (ch == ' ' || ch == '-' || ch == '_')
                sb.Append('-');
        }
        return sb.ToString().Trim('-');
    }
}

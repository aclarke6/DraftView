using System.Text;

namespace DraftView.Infrastructure.Parsing;

/// <summary>
/// Generates a CSS block from a set of parsed Scrivener styles.
/// The output is suitable for embedding in a style tag or writing to a
/// .css file that the reader view links to.
///
/// Default rules provide sensible fallbacks; the author can override them
/// via the project stylesheet once the class names are known.
/// </summary>
public static class ScrivenerStylesCssGenerator
{
    public static string Generate(IReadOnlyDictionary<int, ScrivenerStyle> styles)
    {
        if (styles.Count == 0)
            return string.Empty;

        var sb = new StringBuilder();
        sb.AppendLine("/* Scrivener character styles — auto-generated from Settings/styles.xml */");
        sb.AppendLine("/* Override these rules in your project stylesheet as needed.          */");
        sb.AppendLine();

        foreach (var style in styles.Values.OrderBy(s => s.Id))
        {
            sb.AppendLine($"/* Style {style.Id}: {style.Name} ({style.Type}) */");

            if (style.Type == "paragraph")
            {
                sb.AppendLine($".prose .{style.CssClassName} {{");
                sb.AppendLine($"    /* paragraph style: add block-level overrides here */");
                sb.AppendLine($"}}");
            }
            else
            {
                sb.AppendLine($".prose .{style.CssClassName} {{");
                sb.AppendLine($"    /* character style: add inline overrides here */");
                sb.AppendLine($"}}");
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }
}

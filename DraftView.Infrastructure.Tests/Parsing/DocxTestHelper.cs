using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DraftView.Infrastructure.Tests.Parsing;

/// <summary>
/// Builds minimal in-memory .docx streams for parser tests.
/// Not for production use.
/// </summary>
internal static class DocxTestHelper
{
    /// <summary>
    /// Builds a docx with one paragraph per entry, using the given style and text.
    /// styleId should be "Heading1", "Heading2", "Heading3", or "Normal".
    /// </summary>
    public static Stream BuildDocx(IEnumerable<(string styleId, string text)> paragraphs)
    {
        var ms = new MemoryStream();

        using (var doc = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document, true))
        {
            var main = doc.AddMainDocumentPart();
            main.Document = new Document(new Body());

            foreach (var (styleId, text) in paragraphs)
            {
                var para = new Paragraph(new Run(new Text(text)));
                if (styleId != "Normal")
                {
                    para.ParagraphProperties = new ParagraphProperties(
                        new ParagraphStyleId { Val = styleId });
                }
                main.Document.Body!.Append(para);
            }

            main.Document.Save();
        }

        ms.Position = 0;
        return ms;
    }

    /// <summary>
    /// Builds a docx with a single paragraph containing the given formatted runs.
    /// </summary>
    public static Stream BuildDocxWithFormattedRuns(
        IEnumerable<(bool bold, bool italic, string text)> runs)
    {
        var ms = new MemoryStream();

        using (var doc = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document, true))
        {
            var main = doc.AddMainDocumentPart();
            main.Document = new Document(new Body());

            var para = new Paragraph();
            foreach (var (bold, italic, text) in runs)
            {
                var rpr = new RunProperties();
                if (bold)   rpr.Append(new Bold());
                if (italic) rpr.Append(new Italic());

                var run = new Run(new Text(text) { Space = SpaceProcessingModeValues.Preserve });
                if (bold || italic) run.RunProperties = rpr;

                para.Append(run);
            }

            main.Document.Body!.Append(para);
            main.Document.Save();
        }

        ms.Position = 0;
        return ms;
    }

    /// <summary>
    /// Builds a docx containing a single 1x2 table — used to verify complex elements are stripped.
    /// </summary>
    public static Stream BuildDocxWithTable(string cell1Text, string cell2Text)
    {
        var ms = new MemoryStream();

        using (var doc = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document, true))
        {
            var main = doc.AddMainDocumentPart();
            main.Document = new Document(new Body());

            var table = new Table();
            var row   = new TableRow();
            row.Append(MakeCell(cell1Text));
            row.Append(MakeCell(cell2Text));
            table.Append(row);

            main.Document.Body!.Append(table);
            main.Document.Save();
        }

        ms.Position = 0;
        return ms;
    }

    private static TableCell MakeCell(string text) =>
        new TableCell(new Paragraph(new Run(new Text(text))));
}

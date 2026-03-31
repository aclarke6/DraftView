using System.Diagnostics;
using System.Text;
using DraftView.Domain.Interfaces.Services;
using DraftView.Infrastructure.Parsing;
using DraftView.DevTools;

// ---------------------------------------------------------------------------
// --import mode
// ---------------------------------------------------------------------------
if (args.Length > 0 && args[0] == "--import")
{
    var connString  = args.Length > 1 ? args[1] : throw new ArgumentException("Connection string required.");
    var jsonPath    = args.Length > 2 ? args[2] : @"C:\Users\alast\source\repos\DraftView\betabooks-export.json";
    var authorEmail = args.Length > 3 ? args[3] : "ajclarke@myyahoo.com";
    return await BetaBooksImporter.RunAsync(connString, jsonPath, authorEmail);
}

// ---------------------------------------------------------------------------
// DraftView DevTools - Scrivener project parser test harness
// ---------------------------------------------------------------------------

var scrivPath = args.Length > 0
    ? args[0]
    : @"C:\Users\alast\Dropbox\Apps\Scrivener\Test.scriv";

var openBrowser = !args.Contains("--no-browser");

Console.OutputEncoding = Encoding.UTF8;

Banner("DraftView DevTools");
Console.WriteLine($"Target: {scrivPath}");
Console.WriteLine();

if (!Directory.Exists(scrivPath))
{
    Error($"Directory not found: {scrivPath}");
    return 1;
}

var scrivxFiles = Directory.GetFiles(scrivPath, "*.scrivx");
if (scrivxFiles.Length == 0)
{
    Error("No .scrivx file found in the target directory.");
    return 1;
}

var scrivxPath = scrivxFiles[0];
Console.WriteLine($"Found: {Path.GetFileName(scrivxPath)}");
Console.WriteLine();

// ---------------------------------------------------------------------------
// Step 1: Parse project.scrivx
// ---------------------------------------------------------------------------
Banner("Step 1: Parsing project.scrivx");

ParsedProject parsed;
try
{
    var parser = new ScrivenerProjectParser();
    parsed = parser.Parse(scrivxPath);
    Success("Parsed successfully.");
}
catch (Exception ex)
{
    Error($"Parse failed: {ex.Message}");
    return 1;
}

// ---------------------------------------------------------------------------
// Step 2: Show status map
// ---------------------------------------------------------------------------
Banner("Step 2: Status map");
if (parsed.StatusMap.Count == 0)
{
    Console.WriteLine("  (no status items found)");
}
else
{
    foreach (var kvp in parsed.StatusMap.OrderBy(k => k.Key))
        Console.WriteLine($"  [{kvp.Key,3}] {kvp.Value}");
}
Console.WriteLine();

// ---------------------------------------------------------------------------
// Step 3: Print binder tree
// ---------------------------------------------------------------------------
Banner("Step 3: Binder tree (Manuscript only)");
if (parsed.ManuscriptRoot is null)
{
    Error("No DraftFolder (Manuscript) found.");
    return 1;
}

PrintTree(parsed.ManuscriptRoot, indent: 0);
Console.WriteLine();

// ---------------------------------------------------------------------------
// Step 4: Count nodes
// ---------------------------------------------------------------------------
Banner("Step 4: Node summary");
var allNodes  = Flatten(parsed.ManuscriptRoot).ToList();
var folders   = allNodes.Count(n => n.NodeType == ParsedNodeType.Folder);
var documents = allNodes.Count(n => n.NodeType == ParsedNodeType.Document);
Console.WriteLine($"  Total nodes : {allNodes.Count}");
Console.WriteLine($"  Folders     : {folders}");
Console.WriteLine($"  Documents   : {documents}");
Console.WriteLine();

// ---------------------------------------------------------------------------
// Step 5: Batch conversion + HTML preview files
// ---------------------------------------------------------------------------
Banner("Step 5: Batch conversion and HTML preview");
Console.WriteLine($"  Converting {documents} document node(s)...");
Console.WriteLine();

var converter    = new RtfConverter();
var successCount = 0;
var emptyCount   = 0;
var failCount    = 0;
var previewFiles = new List<(string Title, string FilePath)>();

var tempDir = Path.Combine(Path.GetTempPath(), "DraftViewPreview");
Directory.CreateDirectory(tempDir);

foreach (var old in Directory.GetFiles(tempDir, "*.html"))
    File.Delete(old);

foreach (var doc in allNodes.Where(n => n.NodeType == ParsedNodeType.Document))
{
    try
    {
        var result = await converter.ConvertAsync(scrivPath, doc.Uuid);
        if (result is null)
        {
            emptyCount++;
            Console.WriteLine($"  [EMPTY ] {doc.Title}");
        }
        else
        {
            successCount++;
            Console.WriteLine($"  [OK    ] {doc.Title} - {result.Html.Length} chars, hash: {result.Hash[..12]}...");

            var safeName = string.Concat(doc.Title.Split(Path.GetInvalidFileNameChars()));
            var fileName = $"{successCount:D2}_{safeName}.html";
            var filePath = Path.Combine(tempDir, fileName);
            await File.WriteAllTextAsync(filePath, BuildPreviewPage(doc.Title, result.Html), Encoding.UTF8);
            previewFiles.Add((doc.Title, filePath));
        }
    }
    catch (Exception ex)
    {
        failCount++;
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"  [FAILED] {doc.Title} - {ex.Message}");
        Console.ResetColor();
    }
}

Console.WriteLine();
Console.WriteLine($"  Converted : {successCount}");
Console.WriteLine($"  Empty     : {emptyCount}");
Console.WriteLine($"  Failed    : {failCount}");
Console.WriteLine();

// ---------------------------------------------------------------------------
// Step 6: Write index page and open in browser
// ---------------------------------------------------------------------------
if (previewFiles.Count > 0)
{
    Banner("Step 6: Browser preview");

    var indexPath = Path.Combine(tempDir, "00_index.html");
    await File.WriteAllTextAsync(indexPath, BuildIndexPage(previewFiles), Encoding.UTF8);

    Console.WriteLine($"  Preview files written to:");
    Console.WriteLine($"  {tempDir}");
    Console.WriteLine();

    if (openBrowser)
    {
        Console.WriteLine("  Opening index in default browser...");
        Process.Start(new ProcessStartInfo { FileName = indexPath, UseShellExecute = true });
        Success("Browser opened.");
    }
    else
    {
        Console.WriteLine($"  Open manually: {indexPath}");
    }
}

Console.WriteLine();

if (failCount == 0)
    Success("All conversions completed without errors.");
else
    Error($"{failCount} conversion(s) failed.");

return failCount > 0 ? 1 : 0;

// ---------------------------------------------------------------------------
// HTML builders - using string concatenation to avoid raw string brace issues
// ---------------------------------------------------------------------------

string BuildPreviewPage(string title, string bodyHtml)
{
    var encoded = System.Net.WebUtility.HtmlEncode(title);
    var sb = new StringBuilder();
    sb.AppendLine("<!DOCTYPE html>");
    sb.AppendLine("<html lang=\"en\">");
    sb.AppendLine("<head>");
    sb.AppendLine("  <meta charset=\"UTF-8\">");
    sb.AppendLine("  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
    sb.AppendLine($"  <title>{encoded}</title>");
    sb.AppendLine("  <style>");
    sb.AppendLine("    body { font-family: Georgia, 'Times New Roman', serif; font-size: 16px;");
    sb.AppendLine("           line-height: 1.8; max-width: 720px; margin: 60px auto;");
    sb.AppendLine("           padding: 0 24px; color: #1a1a1a; background: #fafaf8; }");
    sb.AppendLine("    h1   { font-size: 1.2em; font-weight: normal; color: #666;");
    sb.AppendLine("           border-bottom: 1px solid #ddd; padding-bottom: 12px; margin-bottom: 32px; }");
    sb.AppendLine("    p    { margin: 0 0 1em 0; text-indent: 0 !important; }");
    sb.AppendLine("    div  { font-family: Georgia, serif !important;");
    sb.AppendLine("           font-size: 16px !important; }");
    sb.AppendLine("    a.back { display: block; margin-top: 48px; color: #888;");
    sb.AppendLine("             font-size: 0.85em; text-decoration: none; }");
    sb.AppendLine("    a.back:hover { color: #333; }");
    sb.AppendLine("  </style>");
    sb.AppendLine("</head>");
    sb.AppendLine("<body>");
    sb.AppendLine($"  <h1>{encoded}</h1>");
    sb.AppendLine("  <div class=\"content\">");
    sb.AppendLine(bodyHtml);
    sb.AppendLine("  </div>");
    sb.AppendLine("  <a class=\"back\" href=\"00_index.html\">&larr; Back to index</a>");
    sb.AppendLine("</body>");
    sb.AppendLine("</html>");
    return sb.ToString();
}

string BuildIndexPage(List<(string Title, string FilePath)> files)
{
    var sb = new StringBuilder();
    sb.AppendLine("<!DOCTYPE html>");
    sb.AppendLine("<html lang=\"en\">");
    sb.AppendLine("<head>");
    sb.AppendLine("  <meta charset=\"UTF-8\">");
    sb.AppendLine("  <title>DraftView Preview</title>");
    sb.AppendLine("  <style>");
    sb.AppendLine("    body { font-family: Georgia, serif; max-width: 600px;");
    sb.AppendLine("           margin: 60px auto; padding: 0 24px;");
    sb.AppendLine("           background: #fafaf8; color: #1a1a1a; }");
    sb.AppendLine("    h1   { font-size: 1.4em; border-bottom: 1px solid #ddd; padding-bottom: 12px; }");
    sb.AppendLine("    ul   { list-style: none; padding: 0; }");
    sb.AppendLine("    li   { margin: 12px 0; }");
    sb.AppendLine("    a    { color: #2a6496; text-decoration: none; font-size: 1.05em; }");
    sb.AppendLine("    a:hover { text-decoration: underline; }");
    sb.AppendLine("  </style>");
    sb.AppendLine("</head>");
    sb.AppendLine("<body>");
    sb.AppendLine("  <h1>DraftView - Scene Preview</h1>");
    sb.AppendLine("  <ul>");
    foreach (var (title, filePath) in files)
    {
        var fileName = Path.GetFileName(filePath);
        var encoded  = System.Net.WebUtility.HtmlEncode(title);
        sb.AppendLine($"    <li><a href=\"{fileName}\">{encoded}</a></li>");
    }
    sb.AppendLine("  </ul>");
    sb.AppendLine("</body>");
    sb.AppendLine("</html>");
    return sb.ToString();
}

// ---------------------------------------------------------------------------
// Helper functions
// ---------------------------------------------------------------------------

void PrintTree(ParsedBinderNode node, int indent)
{
    var prefix  = new string(' ', indent * 2);
    var typeTag = node.NodeType == ParsedNodeType.Folder ? "[F]" : "[D]";
    var status  = node.ScrivenerStatus is not null ? $" ({node.ScrivenerStatus})" : "";
    Console.WriteLine($"{prefix}{typeTag} {node.Title}{status}");
    foreach (var child in node.Children)
        PrintTree(child, indent + 1);
}

IEnumerable<ParsedBinderNode> Flatten(ParsedBinderNode node)
{
    yield return node;
    foreach (var child in node.Children)
        foreach (var n in Flatten(child))
            yield return n;
}

void Banner(string text)
{
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine($"--- {text} ---");
    Console.ResetColor();
}

void Success(string text)
{
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine($"  {text}");
    Console.ResetColor();
}

void Error(string text)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"  ERROR: {text}");
    Console.ResetColor();
}


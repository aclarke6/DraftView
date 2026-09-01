// One-time backfill: sets ReaderSnapshot + IsRead for Hilary and Becca
// against the closest Scrivener snapshot on or before 25 March 2026.
// Run: dotnet run --project DraftView.DevTools backfill-reader-snapshots [--scriv PATH] [--dry-run]
// Issue #128 -- delete after running on production.
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Npgsql;
using RtfPipe;

namespace DraftView.DevTools;

internal static class ReaderSnapshotBackfill
{
    private static readonly DateTime Cutoff = new(2026, 3, 25, 23, 59, 59, DateTimeKind.Utc);

    private static readonly Regex ScrivCharStyleOpen  = new(@"<\$Scr_Cs::\d+>",     RegexOptions.Compiled);
    private static readonly Regex ScrivCharStyleClose = new(@"</\$Scr_Cs::\d+>",    RegexOptions.Compiled);
    private static readonly Regex ScrivParaStyleOpen  = new(@"<\$Scr_Ps::\d+>",     RegexOptions.Compiled);
    private static readonly Regex ScrivParaStyleClose = new(@"<[!/]\$Scr_Ps::\d+>", RegexOptions.Compiled);
    private static readonly Regex InlineStyle         = new(@" style=""[^""]*""",    RegexOptions.Compiled);

    // Target readers by display name
    private static readonly string[] TargetDisplayNames = ["Hilary Royston-Bishop", "Becca Dunlop"];

    public static async Task<int> RunAsync(string[] args)
    {
        var scrivPath = GetArg(args, "--scriv")
            ?? "/var/www/draftview-cache/ba3d5ee5-61a9-48a8-8901-aa097d1a4fe1/the fractured lattice.scriv";
        var dryRun = args.Contains("--dry-run");

        if (!Directory.Exists(scrivPath))
        {
            Console.WriteLine($"ERROR: Scrivener project not found: {scrivPath}");
            return 1;
        }

        var connString = GetArg(args, "--connection")
            ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? "Host=localhost;Database=draftview;Username=draftview;Password=xuqgeg-posGys-9zafby";

        Console.WriteLine($"Scriv path : {scrivPath}");
        Console.WriteLine($"Cutoff     : {Cutoff:yyyy-MM-dd}");
        Console.WriteLine($"Dry run    : {dryRun}");
        Console.WriteLine();

        var readers = await GetTargetReadersAsync(connString);
        if (readers.Count == 0)
        {
            Console.WriteLine("ERROR: No target readers found in database.");
            return 1;
        }

        Console.WriteLine("Target readers:");
        foreach (var r in readers)
            Console.WriteLine($"  {r.DisplayName} ({r.UserId})");
        Console.WriteLine();

        var sections = await GetPublishedSectionsAsync(connString);
        Console.WriteLine($"Published document sections: {sections.Count}");
        Console.WriteLine();

        int inserted = 0, skippedSame = 0, skippedNoSnapshot = 0, errors = 0;

        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        foreach (var reader in readers)
        {
            Console.WriteLine($"--- Processing {reader.DisplayName} ---");

            foreach (var section in sections)
            {
                try
                {
                    var snapshot = FindLatestSnapshotOnOrBefore(scrivPath, section.ScrivenerUuid);
                    if (snapshot is null)
                    {
                        skippedNoSnapshot++;
                        Console.WriteLine($"  [SKIP-NO-SNAP] {section.ScrivenerUuid}  ({section.Title})");
                        continue;
                    }

                    var (snapshotHtml, _) = await ConvertRtfAsync(snapshot.FilePath);

                    if (snapshotHtml == section.CurrentHtml)
                    {
                        // Content unchanged — still create snapshot so reader shows as "up to date"
                        if (!dryRun)
                            await UpsertReaderSnapshotAndReadEventAsync(
                                connString, section.SectionId, reader.UserId, snapshotHtml, snapshot.Date);

                        skippedSame++;
                        Console.WriteLine($"  [SAME-CONTENT] {section.ScrivenerUuid}  snapshot:{snapshot.Date:yyyy-MM-dd}  ({section.Title})");
                        inserted++;
                        continue;
                    }

                    if (!dryRun)
                        await UpsertReaderSnapshotAndReadEventAsync(
                            connString, section.SectionId, reader.UserId, snapshotHtml, snapshot.Date);

                    inserted++;
                    Console.WriteLine($"  [OK          ] {section.ScrivenerUuid}  snapshot:{snapshot.Date:yyyy-MM-dd}  ({section.Title})");
                }
                catch (Exception ex)
                {
                    errors++;
                    Console.WriteLine($"  [ERROR       ] {section.ScrivenerUuid}  {ex.Message}");
                }
            }

            Console.WriteLine();
        }

        Console.WriteLine($"Snapshots written : {inserted} (across {readers.Count} reader(s))");
        Console.WriteLine($"No snapshot found : {skippedNoSnapshot}");
        Console.WriteLine($"Errors            : {errors}");

        if (dryRun)
        {
            Console.WriteLine();
            Console.WriteLine("[DRY RUN] No changes were written. Re-run without --dry-run to apply.");
        }

        return errors > 0 ? 1 : 0;
    }

    private static async Task<List<ReaderRecord>> GetTargetReadersAsync(string connString)
    {
        var results = new List<ReaderRecord>();
        await using var conn = new NpgsqlConnection(connString);
        await conn.OpenAsync();

        // Look up target readers by display name; fall back to finding all beta readers
        // if none match (allows the operator to verify before running).
        var names = TargetDisplayNames.Select(n => $"'{n}'").Aggregate((a, b) => $"{a},{b}");
        var sql   = $"""
            SELECT "Id", "DisplayName"
            FROM   "AppUsers"
            WHERE  "DisplayName" IN ({names})
              AND  "Role"        = 'BetaReader'
              AND  "IsSoftDeleted" = false
            ORDER BY "DisplayName"
            """;

        await using var cmd    = new NpgsqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            results.Add(new ReaderRecord(reader.GetGuid(0), reader.GetString(1)));

        return results;
    }

    private static async Task<List<SectionRecord>> GetPublishedSectionsAsync(string connString)
    {
        var results = new List<SectionRecord>();
        await using var conn = new NpgsqlConnection(connString);
        await conn.OpenAsync();

        const string sql = """
            SELECT s."Id", s."ScrivenerUuid", s."Title", s."HtmlContent"
            FROM   "Sections" s
            JOIN   "Projects" p ON s."ProjectId" = p."Id"
            WHERE  s."NodeType"    = 'Document'
              AND  s."IsPublished" = true
              AND  s."IsSoftDeleted" = false
              AND  p."ProjectType" = 0
            ORDER BY s."Id"
            """;

        await using var cmd    = new NpgsqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(new SectionRecord(
                SectionId:      reader.GetGuid(0),
                ScrivenerUuid:  reader.GetString(1),
                Title:          reader.GetString(2),
                CurrentHtml:    reader.IsDBNull(3) ? string.Empty : reader.GetString(3)));
        }

        return results;
    }

    private static async Task UpsertReaderSnapshotAndReadEventAsync(
        string connString,
        Guid sectionId,
        Guid userId,
        string htmlContent,
        DateTime snapshotDate)
    {
        await using var conn = new NpgsqlConnection(connString);
        await conn.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();

        // Upsert ReaderSnapshot (one per sectionId+userId pair)
        const string snapshotSql = """
            INSERT INTO "ReaderSnapshots" ("Id", "SectionId", "UserId", "HtmlContent", "SnapshotAt")
            VALUES (@id, @sectionId, @userId, @html, @snapshotAt)
            ON CONFLICT ("SectionId", "UserId")
            DO UPDATE SET "HtmlContent" = EXCLUDED."HtmlContent",
                          "SnapshotAt"  = EXCLUDED."SnapshotAt"
            """;
        await using var snapCmd = new NpgsqlCommand(snapshotSql, conn, tx);
        snapCmd.Parameters.AddWithValue("id",         Guid.NewGuid());
        snapCmd.Parameters.AddWithValue("sectionId",  sectionId);
        snapCmd.Parameters.AddWithValue("userId",     userId);
        snapCmd.Parameters.AddWithValue("html",       htmlContent);
        snapCmd.Parameters.AddWithValue("snapshotAt", snapshotDate);
        await snapCmd.ExecuteNonQueryAsync();

        // Upsert ReadEvent — create if absent, set IsRead=true in all cases
        const string eventSql = """
            INSERT INTO "ReadEvents" ("Id", "SectionId", "UserId", "OpenCount",
                                      "FirstOpenedAt", "LastOpenedAt", "IsRead",
                                      "LastMarkedReadAt", "ResumeAnchorId")
            VALUES (@id, @sectionId, @userId, 1,
                    @now, @now, true, @now, null)
            ON CONFLICT ("SectionId", "UserId")
            DO UPDATE SET "IsRead" = true, "LastMarkedReadAt" = @now
            """;
        await using var eventCmd = new NpgsqlCommand(eventSql, conn, tx);
        eventCmd.Parameters.AddWithValue("id",        Guid.NewGuid());
        eventCmd.Parameters.AddWithValue("sectionId", sectionId);
        eventCmd.Parameters.AddWithValue("userId",    userId);
        eventCmd.Parameters.AddWithValue("now",       snapshotDate);
        await eventCmd.ExecuteNonQueryAsync();

        await tx.CommitAsync();
    }

    private static SnapshotFile? FindLatestSnapshotOnOrBefore(string scrivPath, string uuid)
    {
        // Case-insensitive search for Snapshots directory (Linux fs is case-sensitive)
        var scrivDir = new DirectoryInfo(scrivPath);
        var snapshotsRoot = scrivDir.GetDirectories()
            .FirstOrDefault(d => d.Name.Equals("Snapshots", StringComparison.OrdinalIgnoreCase));
        if (snapshotsRoot is null) return null;

        var snapshotDir = Path.Combine(snapshotsRoot.FullName, $"{uuid.ToLowerInvariant()}.snapshots");
        if (!Directory.Exists(snapshotDir)) return null;

        SnapshotFile? latest = null;

        foreach (var file in Directory.GetFiles(snapshotDir, "*.rtf"))
        {
            var name     = Path.GetFileNameWithoutExtension(file);
            var datePart = name.Split('+')[0];

            if (!DateTime.TryParseExact(datePart, "yyyy-MM-dd-HH-mm-ss",
                    CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var date))
                continue;

            date = DateTime.SpecifyKind(date, DateTimeKind.Utc);
            if (date > Cutoff) continue;
            if (latest is null || date > latest.Date)
                latest = new SnapshotFile(file, date);
        }

        return latest;
    }

    private static async Task<(string Html, string Hash)> ConvertRtfAsync(string rtfPath)
    {
        var rtfBytes = await File.ReadAllBytesAsync(rtfPath);
        var rtfText  = Encoding.UTF8.GetString(rtfBytes);
        rtfText = ScrivCharStyleOpen.Replace(rtfText, string.Empty);
        rtfText = ScrivCharStyleClose.Replace(rtfText, string.Empty);
        rtfText = ScrivParaStyleOpen.Replace(rtfText, string.Empty);
        rtfText = ScrivParaStyleClose.Replace(rtfText, string.Empty);
        var html = Rtf.ToHtml(rtfText);
        html = InlineStyle.Replace(html, string.Empty);
        var hash = Convert.ToHexString(SHA256.HashData(rtfBytes)).ToLowerInvariant();
        return (html, hash);
    }

    private static string? GetArg(string[] args, string name)
    {
        for (var i = 0; i < args.Length - 1; i++)
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        return null;
    }

    private sealed record ReaderRecord(Guid UserId, string DisplayName);
    private sealed record SectionRecord(Guid SectionId, string ScrivenerUuid, string Title, string CurrentHtml);
    private sealed record SnapshotFile(string FilePath, DateTime Date);
}

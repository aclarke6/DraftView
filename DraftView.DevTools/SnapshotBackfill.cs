// TODO: Delete this file after the snapshot backfill has been run on production.
// Issue #117
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using DraftView.Application.Services;
using Microsoft.Extensions.Configuration;
using Npgsql;
using RtfPipe;

namespace DraftView.DevTools;

internal static class SnapshotBackfill
{
    private static readonly DateTime Cutoff = new(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);

    private static readonly Regex ScrivCharStyleOpen  = new(@"<\$Scr_Cs::\d+>",     RegexOptions.Compiled);
    private static readonly Regex ScrivCharStyleClose = new(@"</\$Scr_Cs::\d+>",    RegexOptions.Compiled);
    private static readonly Regex ScrivParaStyleOpen  = new(@"<\$Scr_Ps::\d+>",     RegexOptions.Compiled);
    private static readonly Regex ScrivParaStyleClose = new(@"<[!/]\$Scr_Ps::\d+>", RegexOptions.Compiled);
    private static readonly Regex InlineStyle         = new(@" style=""[^""]*""",    RegexOptions.Compiled);

    public static async Task<int> RunAsync(string[] args)
    {
        var scrivPath = GetArg(args, "--scriv")
            ?? @"C:\Users\alast\Dropbox\Apps\Scrivener\The Fractured Lattice.scriv";
        var dryRun = args.Contains("--dry-run");

        if (!Directory.Exists(scrivPath))
        {
            Console.WriteLine($"ERROR: Scrivener project not found: {scrivPath}");
            return 1;
        }

        const string webSecretsId = "0e437bf4-da42-4cf8-86cd-072126366d5c";
        var config = new ConfigurationBuilder()
            .AddUserSecrets(webSecretsId)
            .AddEnvironmentVariables()
            .Build();

        var connString = config.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection not found in user secrets.");

        Console.WriteLine($"Scriv path : {scrivPath}");
        Console.WriteLine($"Cutoff     : {Cutoff:yyyy-MM-dd}");
        Console.WriteLine($"Dry run    : {dryRun}");
        Console.WriteLine();

        await PrintDiagnosticsAsync(connString);
        var sections = await GetEligibleSectionsAsync(connString);
        Console.WriteLine($"Eligible sections (1 version, ScrivenerDropbox, published): {sections.Count}");
        Console.WriteLine();

        var diffService           = new HtmlDiffService();
        var classificationService = new ChangeClassificationService();

        int inserted = 0, skippedSame = 0, skippedNoSnapshot = 0, errors = 0;

        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        foreach (var section in sections)
        {
            try
            {
                var snapshot = FindLatestSnapshotBefore(scrivPath, section.ScrivenerUuid);
                if (snapshot is null)
                {
                    skippedNoSnapshot++;
                    Console.WriteLine($"  [SKIP-NO-SNAP] {section.ScrivenerUuid}");
                    continue;
                }

                var (historicHtml, historicHash) = await ConvertRtfAsync(snapshot.FilePath);

                if (historicHash == section.CurrentContentHash)
                {
                    skippedSame++;
                    Console.WriteLine($"  [SKIP-SAME   ] {section.ScrivenerUuid}  snapshot:{snapshot.Date:yyyy-MM-dd}");
                    continue;
                }

                var diffParagraphs = diffService.Compute(historicHtml, section.CurrentHtml);
                var classification = classificationService.Classify(diffParagraphs);
                var classInt       = classification.HasValue ? (int?)((int)classification.Value) : null;

                if (!dryRun)
                    await InsertHistoricVersionAsync(connString, section, historicHtml, historicHash, snapshot.Date, classInt);

                inserted++;
                var label = classification?.ToString() ?? "(unclassified)";
                Console.WriteLine($"  [OK          ] {section.ScrivenerUuid}  snapshot:{snapshot.Date:yyyy-MM-dd}  {label}");
            }
            catch (Exception ex)
            {
                errors++;
                Console.WriteLine($"  [ERROR       ] {section.ScrivenerUuid}  {ex.Message}");
            }
        }

        Console.WriteLine();
        Console.WriteLine($"Inserted      : {inserted}");
        Console.WriteLine($"Same content  : {skippedSame}");
        Console.WriteLine($"No snapshot   : {skippedNoSnapshot}");
        Console.WriteLine($"Errors        : {errors}");

        if (dryRun)
        {
            Console.WriteLine();
            Console.WriteLine("[DRY RUN] No changes were written. Re-run without --dry-run to apply.");
        }

        return errors > 0 ? 1 : 0;
    }

    private static async Task PrintDiagnosticsAsync(string connString)
    {
        await using var conn = new NpgsqlConnection(connString);
        await conn.OpenAsync();

        async Task<long> Count(string sql)
        {
            await using var cmd = new NpgsqlCommand(sql, conn);
            return (long)(await cmd.ExecuteScalarAsync() ?? 0L);
        }

        var dbName      = (string)(await new NpgsqlCommand("SELECT current_database()", conn).ExecuteScalarAsync() ?? "?");
        var totalSec    = await Count("SELECT COUNT(*) FROM \"Sections\" WHERE \"NodeType\" = 'Document' AND \"IsPublished\" = true AND \"IsSoftDeleted\" = false");
        var totalVer    = await Count("SELECT COUNT(*) FROM \"SectionVersions\"");
        var secWith1Ver = await Count("SELECT COUNT(*) FROM \"Sections\" s WHERE \"NodeType\" = 'Document' AND \"IsPublished\" = true AND \"IsSoftDeleted\" = false AND (SELECT COUNT(*) FROM \"SectionVersions\" sv WHERE sv.\"SectionId\" = s.\"Id\") = 1");

        Console.WriteLine($"Database   : {dbName}");
        Console.WriteLine($"Published document sections : {totalSec}");
        Console.WriteLine($"Total SectionVersions       : {totalVer}");
        Console.WriteLine($"Sections with exactly 1 ver : {secWith1Ver}");
        Console.WriteLine();
    }

    private static async Task<List<SectionRecord>> GetEligibleSectionsAsync(string connString)
    {
        var results = new List<SectionRecord>();
        await using var conn = new NpgsqlConnection(connString);
        await conn.OpenAsync();

        const string sql = """
            SELECT s."Id", s."ScrivenerUuid", p."AuthorId",
                   sv."Id" AS "VersionId", sv."HtmlContent", sv."ContentHash"
            FROM   "Sections" s
            JOIN   "Projects" p ON s."ProjectId" = p."Id"
            JOIN   "SectionVersions" sv ON sv."SectionId" = s."Id" AND sv."VersionNumber" = 1
            WHERE  s."NodeType"    = 'Document'
              AND  s."IsPublished" = true
              AND  s."IsSoftDeleted" = false
              AND  p."ProjectType" = 0
              AND  (SELECT COUNT(*) FROM "SectionVersions" sv2 WHERE sv2."SectionId" = s."Id") = 1
            ORDER BY s."Id"
            """;

        await using var cmd    = new NpgsqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(new SectionRecord(
                SectionId:          reader.GetGuid(0),
                ScrivenerUuid:      reader.GetString(1),
                AuthorId:           reader.GetGuid(2),
                ExistingVersionId:  reader.GetGuid(3),
                CurrentHtml:        reader.GetString(4),
                CurrentContentHash: reader.GetString(5)));
        }

        return results;
    }

    private static async Task InsertHistoricVersionAsync(
        string connString,
        SectionRecord section,
        string historicHtml,
        string historicHash,
        DateTime snapshotDate,
        int? classificationInt)
    {
        await using var conn = new NpgsqlConnection(connString);
        await conn.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();

        const string updateSql = """
            UPDATE "SectionVersions"
            SET "VersionNumber" = 2, "MinorVersion" = 1, "ChangeClassification" = @class
            WHERE "Id" = @id
            """;
        await using var updateCmd = new NpgsqlCommand(updateSql, conn, tx);
        updateCmd.Parameters.AddWithValue("id", section.ExistingVersionId);
        updateCmd.Parameters.AddWithValue("class",
            classificationInt.HasValue ? (object)classificationInt.Value : DBNull.Value);
        await updateCmd.ExecuteNonQueryAsync();

        const string insertSql = """
            INSERT INTO "SectionVersions"
              ("Id", "SectionId", "AuthorId", "VersionNumber", "MajorVersion", "MinorVersion",
               "ScrivenerStatus", "HtmlContent", "ContentHash", "ChangeClassification", "CreatedAt")
            VALUES
              (@id, @sectionId, @authorId, 1, 1, 0, NULL, @html, @hash, NULL, @createdAt)
            """;
        await using var insertCmd = new NpgsqlCommand(insertSql, conn, tx);
        insertCmd.Parameters.AddWithValue("id",        Guid.NewGuid());
        insertCmd.Parameters.AddWithValue("sectionId", section.SectionId);
        insertCmd.Parameters.AddWithValue("authorId",  section.AuthorId);
        insertCmd.Parameters.AddWithValue("html",      historicHtml);
        insertCmd.Parameters.AddWithValue("hash",      historicHash);
        insertCmd.Parameters.AddWithValue("createdAt", snapshotDate);

        await insertCmd.ExecuteNonQueryAsync();
        await tx.CommitAsync();
    }

    private static SnapshotFile? FindLatestSnapshotBefore(string scrivPath, string uuid)
    {
        var snapshotDir = Path.Combine(scrivPath, "Snapshots", $"{uuid}.snapshots");
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
            if (date >= Cutoff) continue;
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

    private sealed record SectionRecord(
        Guid SectionId, string ScrivenerUuid, Guid AuthorId,
        Guid ExistingVersionId, string CurrentHtml, string CurrentContentHash);

    private sealed record SnapshotFile(string FilePath, DateTime Date);
}

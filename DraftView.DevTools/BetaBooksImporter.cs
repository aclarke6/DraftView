using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;
using DraftView.Infrastructure.Persistence;

namespace DraftView.DevTools;

public static class BetaBooksImporter
{
    public static async Task<int> RunAsync(string connectionString, string jsonPath, string authorEmail, string projectName = "Book 1 - The Fractured Lattice")
    {
        Console.WriteLine("BetaBooks Importer");
        Console.WriteLine("JSON  : " + jsonPath);
        Console.WriteLine("Author: " + authorEmail);
        Console.WriteLine();

        if (!File.Exists(jsonPath))
        {
            Console.WriteLine("ERROR: JSON file not found: " + jsonPath);
            return 1;
        }

        var json   = await File.ReadAllTextAsync(jsonPath);
        var export = JsonSerializer.Deserialize<BetaBooksExport>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("Failed to deserialize JSON.");

        var options = new DbContextOptionsBuilder<DraftViewDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        using var db = new DraftViewDbContext(options);

        var author = await db.AppUsers.FirstOrDefaultAsync(u => u.Email == authorEmail)
            ?? throw new InvalidOperationException("Author not found: " + authorEmail);
        Console.WriteLine("Author found: " + author.DisplayName + " (" + author.Id + ")");

        var project = await db.Projects.FirstOrDefaultAsync(p => p.Name == projectName)
            ?? throw new InvalidOperationException("Project not found: " + projectName);
        Console.WriteLine("Project: " + project.Name + " (" + project.Id + ")");

        var sections = await db.Sections
            .Where(s => s.ProjectId == project.Id && s.NodeType == NodeType.Folder && !s.IsSoftDeleted)
            .ToListAsync();
        Console.WriteLine("Sections loaded: " + sections.Count);
        Console.WriteLine();

        var readerNames = export.Comments
            .Where(c => !string.IsNullOrWhiteSpace(c.Reader))
            .Select(c => c.Reader!)
            .Distinct()
            .ToList();

        var readerMap = new Dictionary<string, Guid>();

        foreach (var name in readerNames)
        {
            var email    = NameToEmail(name);
            var existing = await db.AppUsers.FirstOrDefaultAsync(u => u.Email == email);

            if (existing != null)
            {
                readerMap[name] = existing.Id;
                Console.WriteLine("Reader exists : " + name + " (" + email + ")");
                continue;
            }

            var user = User.Create(email, name, Role.BetaReader);
            user.Activate();
            db.AppUsers.Add(user);

            db.Users.Add(new IdentityUser
            {
                Id                 = Guid.NewGuid().ToString(),
                UserName           = email,
                NormalizedUserName = email.ToUpperInvariant(),
                Email              = email,
                NormalizedEmail    = email.ToUpperInvariant(),
                EmailConfirmed     = true,
                PasswordHash       = "IMPORTED-RESET-REQUIRED",
                SecurityStamp      = Guid.NewGuid().ToString(),
                ConcurrencyStamp   = Guid.NewGuid().ToString(),
                LockoutEnabled     = true
            });

            db.NotificationPreferences.Add(
                UserNotificationPreferences.CreateForBetaReader(user.Id));

            readerMap[name] = user.Id;
            Console.WriteLine("Reader created: " + name + " -> " + email + " (" + user.Id + ")");
        }

        await db.SaveChangesAsync();
        Console.WriteLine();

        var imported = 0;
        var skipped  = 0;
        var notFound = new HashSet<string>();

        foreach (var c in export.Comments)
        {
            if (string.IsNullOrWhiteSpace(c.Body))
            {
                skipped++;
                continue;
            }

            var section = sections.FirstOrDefault(s =>
                s.Title.StartsWith(c.Chapter, StringComparison.OrdinalIgnoreCase));

            if (section == null)
            {
                notFound.Add(c.Chapter);
                skipped++;
                continue;
            }

            if (!readerMap.TryGetValue(c.Reader ?? "", out var readerId))
            {
                skipped++;
                continue;
            }

            var comment = Comment.CreateForImport(
                section.Id, readerId, c.Body,
                Visibility.Public, CommentStatus.New,
                c.PostedAt.HasValue ? DateTime.SpecifyKind(c.PostedAt.Value, DateTimeKind.Utc) : DateTime.UtcNow);

            db.Comments.Add(comment);
            imported++;

            if (c.AuthorReply != null && !string.IsNullOrWhiteSpace(c.AuthorReply.Body))
            {
                var reply = Comment.CreateForImport(
                    section.Id, author.Id, c.AuthorReply.Body,
                    Visibility.Public, CommentStatus.AuthorReply,
                    c.AuthorReply.PostedAt.HasValue ? DateTime.SpecifyKind(c.AuthorReply.PostedAt.Value, DateTimeKind.Utc) : DateTime.UtcNow,
                    comment.Id);

                db.Comments.Add(reply);
                imported++;
            }
        }

        await db.SaveChangesAsync();

        Console.WriteLine("Imported : " + imported + " comments");
        Console.WriteLine("Skipped  : " + skipped);

        if (notFound.Any())
        {
            Console.WriteLine("Chapters not found in DB:");
            foreach (var ch in notFound.OrderBy(x => x))
                Console.WriteLine("  - " + ch);
        }

        Console.WriteLine();
        Console.WriteLine("Done.");
        return 0;
    }

    private static string NameToEmail(string name) => name switch
    {
        "Becca Dunlop"           => "becca@the-dunlops.co.uk",
        "Hilary Royston-Bishop"  => "hilaryrrb@gmail.com",
        _                         => name.ToLowerInvariant().Replace(" ", ".").Replace("-", ".") + "@betabooks.import"
    };
}

public class BetaBooksExport
{
    public string Book { get; set; } = string.Empty;
    public List<BetaBooksComment> Comments { get; set; } = [];
}

public class BetaBooksComment
{
    public string Chapter { get; set; } = string.Empty;
    public string? Reader { get; set; }
    [JsonPropertyName("posted_at")]
    public DateTime? PostedAt { get; set; }
    public string? Body { get; set; }
    [JsonPropertyName("author_reply")]
    public BetaBooksReply? AuthorReply { get; set; }
}

public class BetaBooksReply
{
    [JsonPropertyName("posted_at")]
    public DateTime? PostedAt { get; set; }
    public string? Body { get; set; }
}




using System.IO;
using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;
using DraftView.Infrastructure.Persistence;
using DraftView.Infrastructure.Persistence.Repositories;
using DraftView.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace DraftView.Infrastructure.Tests.Persistence;

public class BetaBooksImporterEmailLookupTests
{
    // 32 bytes of deterministic key material for tests — not real secrets.
    private static readonly byte[] KnownEncKey  = Enumerable.Range(1, 32).Select(i => (byte)i).ToArray();
    private static readonly byte[] KnownHmacKey = Enumerable.Range(33, 32).Select(i => (byte)i).ToArray();

    private DraftViewDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<DraftViewDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options,
            new UserEmailEncryptionService(KnownEncKey),
            new UserEmailLookupHmacService(KnownHmacKey));

    // -------------------------------------------------------------------------

    [Fact]
    public async Task UserLookup_ByEmail_SucceedsWhenContextBuiltWithConfiguredKeys()
    {
        const string email = "author@example.test";

        await using var db = CreateDb();
        var user = User.Create(email, "Test Author", Role.Author);
        db.AppUsers.Add(user);
        await db.SaveChangesAsync();

        var repo   = new UserRepository(db, new UserEmailEncryptionService(KnownEncKey), new UserEmailLookupHmacService(KnownHmacKey));
        var result = await repo.GetByEmailAsync(email);

        Assert.NotNull(result);
        Assert.Equal(user.Id, result!.Id);
    }

    [Fact]
    public async Task TwoContexts_WithSameKeys_ProduceSameHmacForSameEmail()
    {
        const string email = "reader@example.test";

        // First context: create and persist user.
        await using var db1 = CreateDb();
        var user = User.Create(email, "Beta Reader", Role.BetaReader);
        db1.AppUsers.Add(user);
        await db1.SaveChangesAsync();
        var storedHmac = user.EmailLookupHmac;

        // Second context: independently compute HMAC and look up.
        await using var db2 = new DraftViewDbContext(
            new DbContextOptionsBuilder<DraftViewDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options,
            new UserEmailEncryptionService(KnownEncKey),
            new UserEmailLookupHmacService(KnownHmacKey));

        var user2 = User.Create(email, "Beta Reader 2", Role.BetaReader);
        db2.AppUsers.Add(user2);
        await db2.SaveChangesAsync();

        Assert.Equal(storedHmac, user2.EmailLookupHmac);
    }

    [Fact]
    public void BetaBooksImporter_MustNot_DirectlyQueryPlaintextEmail()
    {
        var source = File.ReadAllText(Path.Combine(
            GetSolutionRoot(),
            "DraftView.DevTools",
            "BetaBooksImporter.cs"));

        Assert.DoesNotContain("u.Email ==", source, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UserCreatedByImporter_IsFoundBySubsequentLookup()
    {
        const string email = "becca@the-dunlops.co.uk";

        await using var db = CreateDb();
        var enc  = new UserEmailEncryptionService(KnownEncKey);
        var hmac = new UserEmailLookupHmacService(KnownHmacKey);
        var repo = new UserRepository(db, enc, hmac);

        // Simulate importer creating a reader.
        var reader = User.Create(email, "Becca Dunlop", Role.BetaReader);
        reader.Activate();
        db.AppUsers.Add(reader);
        await db.SaveChangesAsync();

        // Simulate idempotency check: same repo, same context, look up by email.
        var found = await repo.GetByEmailAsync(email);

        Assert.NotNull(found);
        Assert.Equal(reader.Id, found!.Id);
    }

    private static string GetSolutionRoot()
    {
        var dir = Directory.GetCurrentDirectory();
        while (dir != null &&
               !Directory.GetFiles(dir, "*.sln").Any() &&
               !Directory.GetFiles(dir, "*.slnx").Any())
        {
            dir = Directory.GetParent(dir)?.FullName;
        }
        return dir ?? throw new InvalidOperationException("Solution root not found.");
    }
}

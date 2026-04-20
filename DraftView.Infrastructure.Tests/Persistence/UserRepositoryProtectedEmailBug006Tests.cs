using DraftView.Application.Interfaces;
using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;
using DraftView.Infrastructure.Persistence;
using DraftView.Infrastructure.Persistence.Repositories;
using DraftView.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace DraftView.Infrastructure.Tests.Persistence;

public class UserRepositoryProtectedEmailBug006Tests
{
    private static readonly byte[] EncryptionKey = Enumerable.Range(1, 32).Select(i => (byte)i).ToArray();
    private static readonly byte[] HmacKey = Enumerable.Range(33, 32).Select(i => (byte)i).ToArray();

    private static DraftViewDbContext CreateDb(string databaseName)
    {
        var options = new DbContextOptionsBuilder<DraftViewDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        return new DraftViewDbContext(
            options,
            new UserEmailEncryptionService(EncryptionKey),
            new UserEmailLookupHmacService(HmacKey));
    }

    [Fact]
    public async Task DraftViewDbContext_WhenRuntimeEmailIsNotLoaded_AllowsInvalidProtectedEmailToPersist()
    {
        var databaseName = Guid.NewGuid().ToString();

        await using (var setupDb = CreateDb(databaseName))
        {
            var author = User.Create("author.bug006@example.test", "Author", Role.Author);
            author.Activate();
            setupDb.AppUsers.Add(author);
            await setupDb.SaveChangesAsync();
        }

        await using (var mutateDb = CreateDb(databaseName))
        {
            var persistedAuthor = await mutateDb.AppUsers.SingleAsync();
            persistedAuthor.SetProtectedEmail("PENDING-CIPHERTEXT:AUTHOR", "PENDING-HMAC:AUTHOR");
            await mutateDb.SaveChangesAsync();
        }

        await using (var assertDb = CreateDb(databaseName))
        {
            var persistedAuthor = await assertDb.AppUsers.SingleAsync();
            Assert.StartsWith("PENDING-CIPHERTEXT:", persistedAuthor.EmailCiphertext, StringComparison.Ordinal);
            Assert.StartsWith("PENDING-HMAC:", persistedAuthor.EmailLookupHmac, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task GetAuthorAsync_WithInvalidCiphertext_ThrowsInvalidOperationException()
    {
        var databaseName = Guid.NewGuid().ToString();

        await using (var setupDb = CreateDb(databaseName))
        {
            var author = User.Create("author.bug006@example.test", "Author", Role.Author);
            author.Activate();
            setupDb.AppUsers.Add(author);
            await setupDb.SaveChangesAsync();
        }

        await using (var mutateDb = CreateDb(databaseName))
        {
            var persistedAuthor = await mutateDb.AppUsers.SingleAsync();
            persistedAuthor.SetProtectedEmail("PENDING-CIPHERTEXT:AUTHOR", "PENDING-HMAC:AUTHOR");
            await mutateDb.SaveChangesAsync();
        }

        await using var repoDb = CreateDb(databaseName);
        IUserEmailEncryptionService encryptionService = new UserEmailEncryptionService(EncryptionKey);
        IUserEmailLookupHmacService hmacService = new UserEmailLookupHmacService(HmacKey);
        var sut = new UserRepository(repoDb, encryptionService, hmacService);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.GetAuthorAsync());

        Assert.Equal("Ciphertext is not in the expected format.", ex.Message);
    }

    [Fact]
    public async Task GetAllBetaReadersAsync_ExcludesSoftDeletedReaders()
    {
        var databaseName = Guid.NewGuid().ToString();

        await using (var setupDb = CreateDb(databaseName))
        {
            var activeReader = User.Create("active.reader@example.test", "Active Reader", Role.BetaReader);
            activeReader.Activate();
            activeReader.SetProtectedEmail("ACTIVE-CIPHERTEXT", "ACTIVE-HMAC");

            var softDeletedReader = User.Create("deleted.reader@example.test", "Deleted Reader", Role.BetaReader);
            softDeletedReader.Activate();
            softDeletedReader.SoftDelete();
            softDeletedReader.SetProtectedEmail("DELETED-CIPHERTEXT", "DELETED-HMAC");

            setupDb.AppUsers.Add(activeReader);
            setupDb.AppUsers.Add(softDeletedReader);
            await setupDb.SaveChangesAsync();
        }

        await using var repoDb = CreateDb(databaseName);
        IUserEmailEncryptionService encryptionService = new UserEmailEncryptionService(EncryptionKey);
        IUserEmailLookupHmacService hmacService = new UserEmailLookupHmacService(HmacKey);
        var sut = new UserRepository(repoDb, encryptionService, hmacService);

        var readers = await sut.GetAllBetaReadersAsync();

        Assert.Single(readers);
        Assert.Equal("Active Reader", readers[0].DisplayName);
    }
}

using System.IO;
using System.Linq;
using DraftView.Domain.Entities;
using DraftView.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DraftView.Infrastructure.Tests.Persistence;

public class ProtectedEmailPersistenceContractTests
{
    private static DraftViewDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<DraftViewDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new DraftViewDbContext(options);
    }

    [Fact]
    public void UserModel_Must_Map_EmailCiphertext_And_EmailLookupHmac()
    {
        using var db = CreateDb();
        var userEntity = db.Model.FindEntityType(typeof(User));

        Assert.NotNull(userEntity);
        Assert.NotNull(userEntity!.FindProperty("EmailCiphertext"));
        Assert.NotNull(userEntity.FindProperty("EmailLookupHmac"));
    }

    [Fact]
    public void UserModel_Must_Not_Map_Plaintext_Email()
    {
        using var db = CreateDb();
        var userEntity = db.Model.FindEntityType(typeof(User));

        Assert.NotNull(userEntity);
        Assert.Null(userEntity!.FindProperty(nameof(User.Email)));
    }

    [Fact]
    public void UserModel_Must_Have_Unique_Index_On_EmailLookupHmac()
    {
        using var db = CreateDb();
        var userEntity = db.Model.FindEntityType(typeof(User));

        Assert.NotNull(userEntity);

        var index = userEntity!.GetIndexes()
            .FirstOrDefault(i => i.Properties.Any(p => p.Name == "EmailLookupHmac"));

        Assert.NotNull(index);
        Assert.True(index!.IsUnique);
    }

    [Fact]
    public async Task CreatingUser_Must_Not_Persist_PlaintextEmail_In_Mapped_StringProperties()
    {
        using var db = CreateDb();
        const string email = "new.user@example.test";

        var user = User.Create(email, "New User", DraftView.Domain.Enumerations.Role.BetaReader);
        db.AppUsers.Add(user);
        await db.SaveChangesAsync();

        var mappedStringValues = db.Entry(user).Properties
            .Where(p => p.Metadata.ClrType == typeof(string))
            .Select(p => p.CurrentValue?.ToString())
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .ToList();

        Assert.DoesNotContain(email, mappedStringValues, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdatingUserEmail_Must_Not_Persist_PlaintextEmail_In_Mapped_StringProperties()
    {
        using var db = CreateDb();
        const string originalEmail = "original@example.test";
        const string updatedEmail = "updated@example.test";

        var user = User.Create(originalEmail, "Existing User", DraftView.Domain.Enumerations.Role.BetaReader);
        db.AppUsers.Add(user);
        await db.SaveChangesAsync();

        user.UpdateEmail(updatedEmail);
        await db.SaveChangesAsync();

        var mappedStringValues = db.Entry(user).Properties
            .Where(p => p.Metadata.ClrType == typeof(string))
            .Select(p => p.CurrentValue?.ToString())
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .ToList();

        Assert.DoesNotContain(updatedEmail, mappedStringValues, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void UserRepository_EmailLookups_Must_Not_Query_PlaintextEmail()
    {
        var source = File.ReadAllText(Path.Combine(
            GetSolutionRoot(),
            "DraftView.Infrastructure",
            "Persistence",
            "Repositories",
            "UserRepository.cs"));

        Assert.DoesNotContain("u => u.Email == email", source, StringComparison.Ordinal);
    }

    [Fact]
    public void UserConfiguration_Must_Not_Index_Or_Map_PlaintextEmail()
    {
        var source = File.ReadAllText(Path.Combine(
            GetSolutionRoot(),
            "DraftView.Infrastructure",
            "Persistence",
            "Configurations",
            "UserConfiguration.cs"));

        Assert.DoesNotContain("Property(u => u.Email)", source, StringComparison.Ordinal);
        Assert.DoesNotContain("HasIndex(u => u.Email)", source, StringComparison.Ordinal);
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

        if (dir is null)
            throw new InvalidOperationException("Solution root not found.");

        return dir;
    }
}

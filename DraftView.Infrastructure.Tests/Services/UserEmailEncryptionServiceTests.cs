using System.Reflection;
using DraftView.Application.Interfaces;

namespace DraftView.Infrastructure.Tests.Services;

public class UserEmailEncryptionServiceTests
{
    [Fact]
    public void EncryptingEmail_Must_Not_Return_Plaintext()
    {
        const string normalizedEmail = "user@example.test";
        var sut = CreateSut();

        var ciphertext = sut.Encrypt(normalizedEmail);

        Assert.NotEqual(normalizedEmail, ciphertext);
        Assert.False(string.IsNullOrWhiteSpace(ciphertext));
    }

    [Fact]
    public void DecryptingCiphertext_Must_Restore_Original_Normalized_Email()
    {
        const string normalizedEmail = "user@example.test";
        var sut = CreateSut();

        var ciphertext = sut.Encrypt(normalizedEmail);
        var decrypted = sut.Decrypt(ciphertext);

        Assert.Equal(normalizedEmail, decrypted);
    }

    [Fact]
    public void Decrypting_Invalid_Ciphertext_Must_Fail_Safely()
    {
        var sut = CreateSut();

        Assert.ThrowsAny<Exception>(() => sut.Decrypt("not-valid-ciphertext"));
    }

    [Fact]
    public void Encryption_Must_Live_Outside_The_Domain_Model()
    {
        var source = File.ReadAllText(Path.Combine(
            GetSolutionRoot(),
            "DraftView.Domain",
            "Entities",
            "User.cs"));

        Assert.DoesNotContain("Encrypt(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Decrypt(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("IDataProtector", source, StringComparison.Ordinal);
        Assert.DoesNotContain("DataProtection", source, StringComparison.Ordinal);
    }

    private static IUserEmailEncryptionService CreateSut()
    {
        var assembly = typeof(DraftView.Infrastructure.Persistence.DraftViewDbContext).Assembly;
        var type = assembly.GetType("DraftView.Infrastructure.Security.UserEmailEncryptionService");

        Assert.NotNull(type);
        Assert.True(typeof(IUserEmailEncryptionService).IsAssignableFrom(type));

        var instance = Activator.CreateInstance(type!, nonPublic: true) ??
                       Activator.CreateInstance(type!);

        Assert.NotNull(instance);

        return Assert.IsAssignableFrom<IUserEmailEncryptionService>(instance);
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

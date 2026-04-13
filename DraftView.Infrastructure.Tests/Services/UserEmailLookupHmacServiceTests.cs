using DraftView.Application.Interfaces;

namespace DraftView.Infrastructure.Tests.Services;

public class UserEmailLookupHmacServiceTests
{
    [Fact]
    public void Same_Normalized_Email_Must_Produce_The_Same_Lookup_Value()
    {
        const string normalizedEmail = "user@example.test";
        var sut = CreateSut();

        var first = sut.Compute(normalizedEmail);
        var second = sut.Compute(normalizedEmail);

        Assert.Equal(first, second);
    }

    [Fact]
    public void Different_Normalized_Emails_Must_Produce_Different_Lookup_Values()
    {
        var sut = CreateSut();

        var first = sut.Compute("user.one@example.test");
        var second = sut.Compute("user.two@example.test");

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Lookup_Value_Must_Not_Equal_Plaintext_Email()
    {
        const string normalizedEmail = "user@example.test";
        var sut = CreateSut();

        var lookup = sut.Compute(normalizedEmail);

        Assert.NotEqual(normalizedEmail, lookup);
        Assert.False(string.IsNullOrWhiteSpace(lookup));
    }

    [Fact]
    public void Hmac_Generation_Must_Live_Outside_The_Domain_Model()
    {
        var source = File.ReadAllText(Path.Combine(
            GetSolutionRoot(),
            "DraftView.Domain",
            "Entities",
            "User.cs"));

        Assert.DoesNotContain("HMACSHA256", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ComputeHash(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SHA256", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("System.Security.Cryptography", source, StringComparison.Ordinal);
    }

    private static IUserEmailLookupHmacService CreateSut()
    {
        var assembly = typeof(DraftView.Infrastructure.Persistence.DraftViewDbContext).Assembly;
        var type = assembly.GetType("DraftView.Infrastructure.Security.UserEmailLookupHmacService");

        Assert.NotNull(type);
        Assert.True(typeof(IUserEmailLookupHmacService).IsAssignableFrom(type));

        var instance = Activator.CreateInstance(type!, nonPublic: true) ??
                       Activator.CreateInstance(type!);

        Assert.NotNull(instance);

        return Assert.IsAssignableFrom<IUserEmailLookupHmacService>(instance);
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

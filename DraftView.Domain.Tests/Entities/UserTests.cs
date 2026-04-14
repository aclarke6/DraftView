using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;
using DraftView.Domain.Exceptions;

namespace DraftView.Domain.Tests.Entities;

public class UserTests
{
    // ---------------------------------------------------------------------------
    // Create
    // ---------------------------------------------------------------------------

    [Fact]
    public void Create_WithValidData_ReturnsUser()
    {
        var user = User.Create("test@example.com", "Test User", Role.BetaReader);

        Assert.NotEqual(Guid.Empty, user.Id);
        Assert.Equal("test@example.com", user.Email);
        Assert.Equal("Test User", user.DisplayName);
        Assert.Equal(Role.BetaReader, user.Role);
        Assert.False(user.IsActive);
        Assert.False(user.IsSoftDeleted);
        Assert.Null(user.ActivatedAt);
        Assert.Null(user.LastLoginAt);
        Assert.Null(user.LastNotificationCheckAt);
        Assert.Null(user.SoftDeletedAt);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithInvalidEmail_ThrowsInvariantViolationException(string? email)
    {
#pragma warning disable CS8604
        var ex = Assert.Throws<InvariantViolationException>(
            () => User.Create(email, "Test User", Role.BetaReader));
#pragma warning restore CS8604

        Assert.Equal("I-EMAIL", ex.InvariantCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithInvalidDisplayName_ThrowsInvariantViolationException(string? displayName)
    {
#pragma warning disable CS8604
        var ex = Assert.Throws<InvariantViolationException>(
            () => User.Create("test@example.com", displayName, Role.BetaReader));
#pragma warning restore CS8604

        Assert.Equal("I-DISPLAYNAME", ex.InvariantCode);
    }

    [Fact]
    public void Create_AuthorRole_SetsRoleCorrectly()
    {
        var user = User.Create("author@example.com", "The Author", Role.Author);

        Assert.Equal(Role.Author, user.Role);
    }

    [Fact]
    public void Create_SystemSupportRole_SetsRoleCorrectly()
    {
        var user = User.Create("support@draftview.co.uk", "DraftView Support", Role.SystemSupport);

        Assert.Equal(Role.SystemSupport, user.Role);
    }

    // ---------------------------------------------------------------------------
    // Activate
    // ---------------------------------------------------------------------------

    [Fact]
    public void Activate_SetsIsActiveTrue_AndRecordsActivatedAt()
    {
        var user = User.Create("test@example.com", "Test User", Role.BetaReader);
        var before = DateTime.UtcNow;

        user.Activate();

        Assert.True(user.IsActive);
        Assert.NotNull(user.ActivatedAt);
        Assert.True(user.ActivatedAt >= before);
    }

    [Fact]
    public void Activate_WhenAlreadyActive_DoesNotChangeActivatedAt()
    {
        var user = User.Create("test@example.com", "Test User", Role.BetaReader);
        user.Activate();
        var firstActivation = user.ActivatedAt;

        user.Activate();

        Assert.Equal(firstActivation, user.ActivatedAt);
    }

    // ---------------------------------------------------------------------------
    // Deactivate
    // ---------------------------------------------------------------------------

    [Fact]
    public void Deactivate_SetsIsActiveFalse()
    {
        var user = User.Create("test@example.com", "Test User", Role.BetaReader);
        user.Activate();

        user.Deactivate();

        Assert.False(user.IsActive);
    }

    [Fact]
    public void Deactivate_WhenUserIsAuthor_ThrowsInvariantViolationException()
    {
        var author = User.Create("author@example.com", "The Author", Role.Author);
        author.Activate();

        var ex = Assert.Throws<InvariantViolationException>(() => author.Deactivate());

        Assert.Equal("I-16", ex.InvariantCode);
    }

    // ---------------------------------------------------------------------------
    // SoftDelete
    // ---------------------------------------------------------------------------

    [Fact]
    public void SoftDelete_SetsFlagsAndRecordsTimestamp()
    {
        var user = User.Create("test@example.com", "Test User", Role.BetaReader);
        var before = DateTime.UtcNow;

        user.SoftDelete();

        Assert.True(user.IsSoftDeleted);
        Assert.NotNull(user.SoftDeletedAt);
        Assert.True(user.SoftDeletedAt >= before);
    }

    [Fact]
    public void SoftDelete_WhenUserIsAuthor_ThrowsInvariantViolationException()
    {
        var author = User.Create("author@example.com", "The Author", Role.Author);

        var ex = Assert.Throws<InvariantViolationException>(() => author.SoftDelete());

        Assert.Equal("I-16", ex.InvariantCode);
    }

    [Fact]
    public void SoftDelete_WhenAlreadyDeleted_DoesNotChangeSoftDeletedAt()
    {
        var user = User.Create("test@example.com", "Test User", Role.BetaReader);
        user.SoftDelete();
        var firstDeletion = user.SoftDeletedAt;

        user.SoftDelete();

        Assert.Equal(firstDeletion, user.SoftDeletedAt);
    }

    // ---------------------------------------------------------------------------
    // RecordLogin
    // ---------------------------------------------------------------------------

    [Fact]
    public void RecordLogin_SetsLastLoginAt()
    {
        var user = User.Create("test@example.com", "Test User", Role.BetaReader);
        user.Activate();
        var before = DateTime.UtcNow;

        user.RecordLogin();

        Assert.NotNull(user.LastLoginAt);
        Assert.True(user.LastLoginAt >= before);
    }

    [Fact]
    public void RecordLogin_WhenInactive_ThrowsUnauthorisedOperationException()
    {
        var user = User.Create("test@example.com", "Test User", Role.BetaReader);

        Assert.Throws<UnauthorisedOperationException>(() => user.RecordLogin());
    }

    [Fact]
    public void RecordLogin_WhenSoftDeleted_ThrowsUnauthorisedOperationException()
    {
        var user = User.Create("test@example.com", "Test User", Role.BetaReader);
        user.SoftDelete();

        Assert.Throws<UnauthorisedOperationException>(() => user.RecordLogin());
    }
    // ---------------------------------------------------------------------------
    // RecordNotificationCheck
    // ---------------------------------------------------------------------------

    [Fact]
    public void RecordNotificationCheck_SetsLastNotificationCheckAt()
    {
        var user = User.Create("test@example.com", "Test User", Role.BetaReader);
        var before = DateTime.UtcNow;

        user.RecordNotificationCheck();

        Assert.NotNull(user.LastNotificationCheckAt);
        Assert.True(user.LastNotificationCheckAt >= before);
    }

    [Fact]
    public void RecordNotificationCheck_CalledTwice_UpdatesTimestamp()
    {
        var user = User.Create("test@example.com", "Test User", Role.BetaReader);
        user.RecordNotificationCheck();
        var first = user.LastNotificationCheckAt;

        System.Threading.Thread.Sleep(10);
        user.RecordNotificationCheck();

        Assert.True(user.LastNotificationCheckAt > first);
    }

    [Fact]
    public void UpdateDisplayName_WithValidName_SetsDisplayName()
    {
        var user = User.Create("test@example.com", "Old Name", Role.BetaReader);
        user.UpdateDisplayName("New Name");
        Assert.Equal("New Name", user.DisplayName);
    }
    [Fact]
    public void UpdateDisplayName_TrimsWhitespace()
    {
        var user = User.Create("test@example.com", "Old Name", Role.BetaReader);
        user.UpdateDisplayName("  New Name  ");
        Assert.Equal("New Name", user.DisplayName);
    }
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void UpdateDisplayName_WithInvalidName_ThrowsInvariantViolationException(string? name)
    {
#pragma warning disable CS8604
        var user = User.Create("test@example.com", "Old Name", Role.BetaReader);
        var ex = Assert.Throws<InvariantViolationException>(() => user.UpdateDisplayName(name));
        Assert.Equal("I-DISPLAYNAME", ex.InvariantCode);
    }
    [Fact]
    public void UpdateEmail_WithValidEmail_SetsEmail()
    {
        var user = User.Create("old@example.com", "Test User", Role.BetaReader);
        user.UpdateEmail("new@example.com");
        Assert.Equal("new@example.com", user.Email);
    }
    [Fact]
    public void UpdateEmail_TrimsWhitespace()
    {
        var user = User.Create("old@example.com", "Test User", Role.BetaReader);
        user.UpdateEmail("  new@example.com  ");
        Assert.Equal("new@example.com", user.Email);
    }
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void UpdateEmail_WithInvalidEmail_ThrowsInvariantViolationException(string? email)
    {
#pragma warning disable CS8604
        var user = User.Create("old@example.com", "Test User", Role.BetaReader);
        var ex = Assert.Throws<InvariantViolationException>(() => user.UpdateEmail(email));
        Assert.Equal("I-EMAIL", ex.InvariantCode);
    }

    // ---------------------------------------------------------------------------
    // Protected Email State
    // ---------------------------------------------------------------------------

    [Fact]
    public void SetProtectedEmail_WithValidProtectedValues_SetsCiphertextAndLookupHmac()
    {
        var user = User.Create("user@example.com", "Test User", Role.BetaReader);

        user.SetProtectedEmail("ciphertext-value", "lookup-hmac-value");

        Assert.Equal("ciphertext-value", user.EmailCiphertext);
        Assert.Equal("lookup-hmac-value", user.EmailLookupHmac);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SetProtectedEmail_WithInvalidCiphertext_ThrowsInvariantViolationException(string? ciphertext)
    {
#pragma warning disable CS8604
        var user = User.Create("user@example.com", "Test User", Role.BetaReader);
        var ex = Assert.Throws<InvariantViolationException>(() => user.SetProtectedEmail(ciphertext, "lookup-hmac-value"));
#pragma warning restore CS8604

        Assert.Equal("I-EMAIL-CIPHERTEXT", ex.InvariantCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SetProtectedEmail_WithInvalidLookupHmac_ThrowsInvariantViolationException(string? lookupHmac)
    {
#pragma warning disable CS8604
        var user = User.Create("user@example.com", "Test User", Role.BetaReader);
        var ex = Assert.Throws<InvariantViolationException>(() => user.SetProtectedEmail("ciphertext-value", lookupHmac));
#pragma warning restore CS8604

        Assert.Equal("I-EMAIL-HMAC", ex.InvariantCode);
    }

    [Fact]
    public void LoadEmailForRuntime_WithValidEmail_SetsRuntimeEmailOnly()
    {
        var user = User.Create("original@example.com", "Test User", Role.BetaReader);
        user.SetProtectedEmail("ciphertext-value", "lookup-hmac-value");

        user.LoadEmailForRuntime("runtime@example.com");

        Assert.Equal("runtime@example.com", user.Email);
        Assert.Equal("ciphertext-value", user.EmailCiphertext);
        Assert.Equal("lookup-hmac-value", user.EmailLookupHmac);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void LoadEmailForRuntime_WithInvalidEmail_ThrowsInvariantViolationException(string? email)
    {
#pragma warning disable CS8604
        var user = User.Create("original@example.com", "Test User", Role.BetaReader);
        var ex = Assert.Throws<InvariantViolationException>(() => user.LoadEmailForRuntime(email));
#pragma warning restore CS8604

        Assert.Equal("I-EMAIL", ex.InvariantCode);
    }
}


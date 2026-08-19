using DraftView.Domain.Entities;
using DraftView.Domain.Exceptions;

namespace DraftView.Domain.Tests.Entities;

/// <summary>
/// Tests for the Account entity.
/// Covers: factory method invariants, activation, soft-delete, login recording,
/// display name and email updates, and protected email state.
/// Excludes: EF persistence, identity integration, email encryption (Infrastructure concerns).
/// </summary>
public class AccountTests
{
    // ---------------------------------------------------------------------------
    // Create
    // ---------------------------------------------------------------------------

    [Fact]
    public void Create_WithValidData_ReturnsAccount()
    {
        var account = Account.Create("test@example.com", "Test User");

        Assert.NotEqual(Guid.Empty, account.Id);
        Assert.Equal("test@example.com", account.Email);
        Assert.Equal("Test User", account.DisplayName);
        Assert.False(account.IsActive);
        Assert.False(account.IsSoftDeleted);
        Assert.Null(account.ActivatedAt);
        Assert.Null(account.LastLoginAt);
        Assert.Null(account.SoftDeletedAt);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithInvalidEmail_ThrowsInvariantViolationException(string? email)
    {
#pragma warning disable CS8604
        var ex = Assert.Throws<InvariantViolationException>(
            () => Account.Create(email, "Test User"));
#pragma warning restore CS8604

        Assert.Equal("I-ACC-EMAIL", ex.InvariantCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithInvalidDisplayName_ThrowsInvariantViolationException(string? displayName)
    {
#pragma warning disable CS8604
        var ex = Assert.Throws<InvariantViolationException>(
            () => Account.Create("test@example.com", displayName));
#pragma warning restore CS8604

        Assert.Equal("I-ACC-DISPLAYNAME", ex.InvariantCode);
    }

    [Fact]
    public void Create_TrimsEmailAndDisplayName()
    {
        var account = Account.Create("  trimmed@example.com  ", "  Trimmed Name  ");

        Assert.Equal("trimmed@example.com", account.Email);
        Assert.Equal("Trimmed Name", account.DisplayName);
    }

    [Fact]
    public void Create_SetsCreatedAtToNow()
    {
        var before = DateTime.UtcNow;
        var account = Account.Create("test@example.com", "Test User");

        Assert.True(account.CreatedAt >= before);
        Assert.True(account.CreatedAt <= DateTime.UtcNow);
    }

    // ---------------------------------------------------------------------------
    // Activate
    // ---------------------------------------------------------------------------

    [Fact]
    public void Activate_SetsIsActiveTrueAndRecordsActivatedAt()
    {
        var account = Account.Create("test@example.com", "Test User");
        var before = DateTime.UtcNow;

        account.Activate();

        Assert.True(account.IsActive);
        Assert.NotNull(account.ActivatedAt);
        Assert.True(account.ActivatedAt >= before);
    }

    [Fact]
    public void Activate_WhenAlreadyActive_DoesNotChangeActivatedAt()
    {
        var account = Account.Create("test@example.com", "Test User");
        account.Activate();
        var firstActivation = account.ActivatedAt;

        account.Activate();

        Assert.Equal(firstActivation, account.ActivatedAt);
    }

    // ---------------------------------------------------------------------------
    // SoftDelete
    // ---------------------------------------------------------------------------

    [Fact]
    public void SoftDelete_SetsFlagsAndRecordsTimestamp()
    {
        var account = Account.Create("test@example.com", "Test User");
        var before = DateTime.UtcNow;

        account.SoftDelete();

        Assert.True(account.IsSoftDeleted);
        Assert.NotNull(account.SoftDeletedAt);
        Assert.True(account.SoftDeletedAt >= before);
    }

    [Fact]
    public void SoftDelete_WhenAlreadyDeleted_DoesNotChangeSoftDeletedAt()
    {
        var account = Account.Create("test@example.com", "Test User");
        account.SoftDelete();
        var firstDeletion = account.SoftDeletedAt;

        account.SoftDelete();

        Assert.Equal(firstDeletion, account.SoftDeletedAt);
    }

    // ---------------------------------------------------------------------------
    // RecordLogin
    // ---------------------------------------------------------------------------

    [Fact]
    public void RecordLogin_WhenActive_SetsLastLoginAt()
    {
        var account = Account.Create("test@example.com", "Test User");
        account.Activate();
        var before = DateTime.UtcNow;

        account.RecordLogin();

        Assert.NotNull(account.LastLoginAt);
        Assert.True(account.LastLoginAt >= before);
    }

    [Fact]
    public void RecordLogin_WhenInactive_ThrowsUnauthorisedOperationException()
    {
        var account = Account.Create("test@example.com", "Test User");

        Assert.Throws<UnauthorisedOperationException>(() => account.RecordLogin());
    }

    [Fact]
    public void RecordLogin_WhenSoftDeleted_ThrowsUnauthorisedOperationException()
    {
        var account = Account.Create("test@example.com", "Test User");
        account.SoftDelete();

        Assert.Throws<UnauthorisedOperationException>(() => account.RecordLogin());
    }

    // ---------------------------------------------------------------------------
    // UpdateDisplayName
    // ---------------------------------------------------------------------------

    [Fact]
    public void UpdateDisplayName_WithValidName_SetsDisplayName()
    {
        var account = Account.Create("test@example.com", "Old Name");

        account.UpdateDisplayName("New Name");

        Assert.Equal("New Name", account.DisplayName);
    }

    [Fact]
    public void UpdateDisplayName_TrimsWhitespace()
    {
        var account = Account.Create("test@example.com", "Old Name");

        account.UpdateDisplayName("  New Name  ");

        Assert.Equal("New Name", account.DisplayName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void UpdateDisplayName_WithInvalidName_ThrowsInvariantViolationException(string? name)
    {
#pragma warning disable CS8604
        var account = Account.Create("test@example.com", "Old Name");
        var ex = Assert.Throws<InvariantViolationException>(() => account.UpdateDisplayName(name));
#pragma warning restore CS8604

        Assert.Equal("I-ACC-DISPLAYNAME", ex.InvariantCode);
    }

    // ---------------------------------------------------------------------------
    // UpdateEmail
    // ---------------------------------------------------------------------------

    [Fact]
    public void UpdateEmail_WithValidEmail_SetsEmail()
    {
        var account = Account.Create("old@example.com", "Test User");

        account.UpdateEmail("new@example.com");

        Assert.Equal("new@example.com", account.Email);
    }

    [Fact]
    public void UpdateEmail_TrimsWhitespace()
    {
        var account = Account.Create("old@example.com", "Test User");

        account.UpdateEmail("  new@example.com  ");

        Assert.Equal("new@example.com", account.Email);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void UpdateEmail_WithInvalidEmail_ThrowsInvariantViolationException(string? email)
    {
#pragma warning disable CS8604
        var account = Account.Create("old@example.com", "Test User");
        var ex = Assert.Throws<InvariantViolationException>(() => account.UpdateEmail(email));
#pragma warning restore CS8604

        Assert.Equal("I-ACC-EMAIL", ex.InvariantCode);
    }

    // ---------------------------------------------------------------------------
    // Protected Email State
    // ---------------------------------------------------------------------------

    [Fact]
    public void SetProtectedEmail_WithValidValues_SetsCiphertextAndLookupHmac()
    {
        var account = Account.Create("test@example.com", "Test User");

        account.SetProtectedEmail("ciphertext-value", "lookup-hmac-value");

        Assert.Equal("ciphertext-value", account.EmailCiphertext);
        Assert.Equal("lookup-hmac-value", account.EmailLookupHmac);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SetProtectedEmail_WithInvalidCiphertext_ThrowsInvariantViolationException(string? ciphertext)
    {
#pragma warning disable CS8604
        var account = Account.Create("test@example.com", "Test User");
        var ex = Assert.Throws<InvariantViolationException>(
            () => account.SetProtectedEmail(ciphertext, "lookup-hmac-value"));
#pragma warning restore CS8604

        Assert.Equal("I-ACC-EMAIL-CIPHERTEXT", ex.InvariantCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SetProtectedEmail_WithInvalidLookupHmac_ThrowsInvariantViolationException(string? lookupHmac)
    {
#pragma warning disable CS8604
        var account = Account.Create("test@example.com", "Test User");
        var ex = Assert.Throws<InvariantViolationException>(
            () => account.SetProtectedEmail("ciphertext-value", lookupHmac));
#pragma warning restore CS8604

        Assert.Equal("I-ACC-EMAIL-HMAC", ex.InvariantCode);
    }

    [Fact]
    public void LoadEmailForRuntime_WithValidEmail_SetsRuntimeEmailOnly()
    {
        var account = Account.Create("original@example.com", "Test User");
        account.SetProtectedEmail("ciphertext-value", "lookup-hmac-value");

        account.LoadEmailForRuntime("runtime@example.com");

        Assert.Equal("runtime@example.com", account.Email);
        Assert.Equal("ciphertext-value", account.EmailCiphertext);
        Assert.Equal("lookup-hmac-value", account.EmailLookupHmac);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void LoadEmailForRuntime_WithInvalidEmail_ThrowsInvariantViolationException(string? email)
    {
#pragma warning disable CS8604
        var account = Account.Create("original@example.com", "Test User");
        var ex = Assert.Throws<InvariantViolationException>(() => account.LoadEmailForRuntime(email));
#pragma warning restore CS8604

        Assert.Equal("I-ACC-EMAIL", ex.InvariantCode);
    }
}

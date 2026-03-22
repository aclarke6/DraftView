namespace ScrivenerSync.Domain.Entities;

public class PasswordResetToken
{
    public Guid   Id        { get; private set; }
    public string Email     { get; private set; } = string.Empty;
    public string Token     { get; private set; } = string.Empty;
    public DateTime ExpiresAt { get; private set; }
    public bool   IsUsed    { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private PasswordResetToken() { }

    public static PasswordResetToken Create(string email)
    {
        return new PasswordResetToken
        {
            Id        = Guid.NewGuid(),
            Email     = email,
            Token     = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32))
                            .Replace("+", "-").Replace("/", "_").Replace("=", ""),
            ExpiresAt = DateTime.UtcNow.AddHours(24),
            IsUsed    = false,
            CreatedAt = DateTime.UtcNow
        };
    }

    public bool IsValid() => !IsUsed && DateTime.UtcNow < ExpiresAt;

    public void MarkUsed() => IsUsed = true;
}

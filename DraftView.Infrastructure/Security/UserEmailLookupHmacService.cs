using DraftView.Application.Interfaces;
using System.Security.Cryptography;
using System.Text;

namespace DraftView.Infrastructure.Security;

public class UserEmailLookupHmacService : IUserEmailLookupHmacService
{
    private const int KeySize = 32;

    private readonly byte[] hmacKey;

    public UserEmailLookupHmacService()
        : this(RandomNumberGenerator.GetBytes(KeySize))
    {
    }

    public UserEmailLookupHmacService(byte[] hmacKey)
    {
        ArgumentNullException.ThrowIfNull(hmacKey);

        if (hmacKey.Length != KeySize)
            throw new ArgumentException($"HMAC key must be {KeySize} bytes.", nameof(hmacKey));

        this.hmacKey = hmacKey.ToArray();
    }

    public string Compute(string normalizedEmail)
    {
        if (string.IsNullOrWhiteSpace(normalizedEmail))
            throw new ArgumentException("Normalized email must not be null or whitespace.", nameof(normalizedEmail));

        var inputBytes = Encoding.UTF8.GetBytes(normalizedEmail);

        using var hmac = new HMACSHA256(hmacKey);
        var hash = hmac.ComputeHash(inputBytes);

        return Convert.ToHexString(hash);
    }
}

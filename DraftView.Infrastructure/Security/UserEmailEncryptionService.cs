using DraftView.Application.Interfaces;
using System.Security.Cryptography;
using System.Text;

namespace DraftView.Infrastructure.Security;

public class UserEmailEncryptionService : IUserEmailEncryptionService
{
    private const int KeySize = 32;
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const byte FormatVersion = 1;

    private readonly byte[] encryptionKey;

    public UserEmailEncryptionService()
        : this(RandomNumberGenerator.GetBytes(KeySize))
    {
    }

    public UserEmailEncryptionService(byte[] encryptionKey)
    {
        ArgumentNullException.ThrowIfNull(encryptionKey);

        if (encryptionKey.Length != KeySize)
            throw new ArgumentException($"Encryption key must be {KeySize} bytes.", nameof(encryptionKey));

        this.encryptionKey = encryptionKey.ToArray();
    }

    public string Encrypt(string normalizedEmail)
    {
        if (string.IsNullOrWhiteSpace(normalizedEmail))
            throw new ArgumentException("Normalized email must not be null or whitespace.", nameof(normalizedEmail));

        var plaintext = Encoding.UTF8.GetBytes(normalizedEmail);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(encryptionKey, TagSize);
        aes.Encrypt(nonce, plaintext, ciphertext, tag);

        var payload = new byte[1 + NonceSize + TagSize + ciphertext.Length];
        payload[0] = FormatVersion;
        Buffer.BlockCopy(nonce, 0, payload, 1, NonceSize);
        Buffer.BlockCopy(tag, 0, payload, 1 + NonceSize, TagSize);
        Buffer.BlockCopy(ciphertext, 0, payload, 1 + NonceSize + TagSize, ciphertext.Length);

        return Convert.ToBase64String(payload);
    }

    public string Decrypt(string ciphertext)
    {
        if (string.IsNullOrWhiteSpace(ciphertext))
            throw new ArgumentException("Ciphertext must not be null or whitespace.", nameof(ciphertext));

        byte[] payload;
        try
        {
            payload = Convert.FromBase64String(ciphertext);
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException("Ciphertext is not in the expected format.", ex);
        }

        if (payload.Length < 1 + NonceSize + TagSize)
            throw new InvalidOperationException("Ciphertext payload is too short.");

        if (payload[0] != FormatVersion)
            throw new InvalidOperationException("Ciphertext format version is not supported.");

        var nonce = payload.AsSpan(1, NonceSize).ToArray();
        var tag = payload.AsSpan(1 + NonceSize, TagSize).ToArray();
        var encryptedBytes = payload.AsSpan(1 + NonceSize + TagSize).ToArray();
        var plaintext = new byte[encryptedBytes.Length];

        try
        {
            using var aes = new AesGcm(encryptionKey, TagSize);
            aes.Decrypt(nonce, encryptedBytes, tag, plaintext);
        }
        catch (CryptographicException ex)
        {
            throw new InvalidOperationException("Ciphertext could not be decrypted.", ex);
        }

        return Encoding.UTF8.GetString(plaintext);
    }
}

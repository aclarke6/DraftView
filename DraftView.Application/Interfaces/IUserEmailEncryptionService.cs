namespace DraftView.Application.Interfaces;

public interface IUserEmailEncryptionService
{
    string Encrypt(string normalizedEmail);
    string Decrypt(string ciphertext);
}

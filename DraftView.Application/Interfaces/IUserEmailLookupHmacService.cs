namespace DraftView.Application.Interfaces;

public interface IUserEmailLookupHmacService
{
    string Compute(string normalizedEmail);
}

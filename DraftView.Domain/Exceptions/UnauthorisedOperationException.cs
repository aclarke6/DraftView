namespace DraftView.Domain.Exceptions;

public sealed class UnauthorisedOperationException : DomainException
{
    public UnauthorisedOperationException(string message) : base(message) { }
}

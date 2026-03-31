namespace DraftView.Domain.Exceptions;

public sealed class InvariantViolationException : DomainException
{
    public string InvariantCode { get; }

    public InvariantViolationException(string invariantCode, string message)
        : base(message)
    {
        InvariantCode = invariantCode;
    }
}

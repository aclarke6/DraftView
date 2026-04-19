namespace DraftView.Domain.Exceptions;

/// <summary>
/// Thrown when a new version cannot be created because the section has
/// reached the retention limit for the author's subscription tier.
/// The author must delete an older version before publishing again.
/// </summary>
public class VersionRetentionLimitException : DomainException
{
    public int Limit { get; }

    public VersionRetentionLimitException(int limit)
        : base($"Version limit of {limit} reached. Delete an older version before publishing again.")
    {
        Limit = limit;
    }
}

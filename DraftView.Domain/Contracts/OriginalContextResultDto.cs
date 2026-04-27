namespace DraftView.Domain.Contracts;

/// <summary>
/// Result of retrieving original context for a passage anchor.
/// </summary>
public sealed class OriginalContextResultDto
{
    public bool Succeeded { get; init; }
    public OriginalContextFailureReason? FailureReason { get; init; }
    public OriginalContextDto? Context { get; init; }

    public static OriginalContextResultDto Success(OriginalContextDto context) =>
        new() { Succeeded = true, Context = context };

    public static OriginalContextResultDto Failure(OriginalContextFailureReason reason) =>
        new() { Succeeded = false, FailureReason = reason };
}

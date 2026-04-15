namespace DraftView.Domain.Exceptions;

public class DuplicateProjectException : Exception
{
    public string SyncRootId
    {
        get;
    }

    public DuplicateProjectException(string syncRootId)
        : base($"A project with SyncRootId '{syncRootId}' already exists.")
    {
        SyncRootId = syncRootId;
    }
}
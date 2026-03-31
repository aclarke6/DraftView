namespace DraftView.Domain.Exceptions;

public class DuplicateProjectException : Exception
{
    public string ScrivenerRootUuid { get; }

    public DuplicateProjectException(string scrivenerRootUuid)
        : base($"A project with ScrivenerRootUuid '{scrivenerRootUuid}' already exists.")
    {
        ScrivenerRootUuid = scrivenerRootUuid;
    }
}

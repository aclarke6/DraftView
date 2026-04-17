namespace DraftView.Domain.Exceptions;

/// <summary>
/// Thrown when an import provider cannot handle the supplied file extension.
/// </summary>
public class UnsupportedFileTypeException : Exception
{
    public string Extension { get; }

    public UnsupportedFileTypeException(string extension)
        : base($"No import provider is registered for file extension '{extension}'.")
    {
        Extension = extension;
    }
}

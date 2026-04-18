namespace DraftView.Infrastructure.Sync;

public interface IPlatformPathService
{
    bool IsWindows { get; }
    bool IsMacOS { get; }
    bool IsLinux { get; }

    string GetFolderPath(Environment.SpecialFolder folder);
}
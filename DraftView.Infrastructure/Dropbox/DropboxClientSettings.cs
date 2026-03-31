namespace DraftView.Infrastructure.Dropbox;

public class DropboxClientSettings
{
    public string AppKey { get; set; } = string.Empty;
    public string AppSecret { get; set; } = string.Empty;
    public string AccessToken { get; set; } = string.Empty;
    public string DropboxScrivenerPath { get; set; } = "/Apps/Scrivener";
    public string LocalCachePath { get; set; } = string.Empty;
}

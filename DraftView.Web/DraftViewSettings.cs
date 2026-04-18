namespace DraftView.Web;

public class DraftViewSettings
{
    public int SyncIntervalMinutes { get; set; } = 5;
    public string DropboxBasePath { get; set; } = string.Empty;
    public string LocalCachePath { get; set; } = string.Empty;

    public string ResolvedDropboxBasePath =>
        string.IsNullOrWhiteSpace(DropboxBasePath)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Dropbox", "Apps", "Scrivener")
            : DropboxBasePath;
}

public class EmailSettings
{
    public string Provider { get; set; } = "Console";
    public SmtpSettings Smtp { get; set; } = new();
}

public class SmtpSettings
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string From { get; set; } = string.Empty;
    public string FromName { get; set; } = "DraftView";
}

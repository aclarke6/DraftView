namespace DraftReader.Web;

public class DraftReaderSettings
{
    public int SyncIntervalMinutes { get; set; } = 5;
    public string DropboxBasePath { get; set; } = string.Empty;
    public string DatabasePath { get; set; } = string.Empty;
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
    public string FromAddress { get; set; } = string.Empty;
    public string FromName { get; set; } = "DraftReader";
}

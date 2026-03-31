namespace DraftView.Domain.Enumerations;

public enum DropboxConnectionStatus
{
    /// <summary>OAuth token valid; sync operational.</summary>
    Connected,

    /// <summary>No OAuth flow completed yet; credentials not configured.</summary>
    NotConnected,

    /// <summary>Access token expired; refresh required.</summary>
    TokenExpired,

    /// <summary>Owner revoked access in Dropbox; re-authorisation required.</summary>
    Revoked,

    /// <summary>Connection error. ErrorMessage populated.</summary>
    Error
}

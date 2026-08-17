namespace DraftView.Web.Infrastructure;

/// <summary>
/// Claim type constants written into the auth cookie during reader impersonation.
/// </summary>
public static class ImpersonationClaims
{
    public const string ImpersonatorEmail     = "draftview:impersonator_email";
    public const string ImpersonatedDisplayName = "draftview:impersonated_name";
}

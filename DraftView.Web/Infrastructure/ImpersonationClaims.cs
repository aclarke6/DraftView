using System.Security.Claims;

namespace DraftView.Web.Infrastructure;

/// <summary>
/// Claim type constants written into the auth cookie during reader impersonation,
/// and helpers for preserving those claims across security stamp refreshes.
/// </summary>
public static class ImpersonationClaims
{
    public const string ImpersonatorEmail       = "draftview:impersonator_email";
    public const string ImpersonatedDisplayName = "draftview:impersonated_name";

    /// <summary>
    /// Copies impersonation claims from <paramref name="oldPrincipal"/> to
    /// <paramref name="newPrincipal"/> so that Pretend mode survives a
    /// SecurityStamp refresh (which re-creates the principal from the database
    /// and would otherwise silently drop the custom claims).
    /// No-op when the old principal carries no impersonation claims.
    /// </summary>
    public static void PreserveImpersonationClaims(
        ClaimsPrincipal? oldPrincipal,
        ClaimsPrincipal  newPrincipal)
    {
        var impersonatorEmail = oldPrincipal?.FindFirstValue(ImpersonatorEmail);
        if (impersonatorEmail is null)
            return;

        var identity = (ClaimsIdentity)newPrincipal.Identity!;
        identity.AddClaim(new Claim(ImpersonatorEmail, impersonatorEmail));

        var impersonatedName = oldPrincipal?.FindFirstValue(ImpersonatedDisplayName);
        if (impersonatedName is not null)
            identity.AddClaim(new Claim(ImpersonatedDisplayName, impersonatedName));
    }
}

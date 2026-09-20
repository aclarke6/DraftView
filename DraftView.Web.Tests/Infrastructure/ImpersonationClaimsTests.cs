using System.Security.Claims;
using DraftView.Web.Infrastructure;
using Xunit;

namespace DraftView.Web.Tests.Infrastructure;

/// <summary>
/// Tests for ImpersonationClaims.PreserveImpersonationClaims.
/// Covers: impersonation claims copied from old to new principal, no-op when
/// old principal has no claims, null old principal handled safely.
/// Excludes: SecurityStampValidator wiring (integration concern).
/// </summary>
public class ImpersonationClaimsTests
{
    [Fact]
    public void PreserveImpersonationClaims_WhenOldPrincipalHasBothClaims_CopiesThemToNewPrincipal()
    {
        // #175 — claims must survive a SecurityStamp refresh so the author
        // does not get permanently logged in as the reader after a server restart.
        var oldPrincipal = MakePrincipal(
            (ImpersonationClaims.ImpersonatorEmail,     "author@example.test"),
            (ImpersonationClaims.ImpersonatedDisplayName, "Hilary"));
        var newPrincipal = MakePrincipal();

        ImpersonationClaims.PreserveImpersonationClaims(oldPrincipal, newPrincipal);

        Assert.Equal("author@example.test",
            newPrincipal.FindFirstValue(ImpersonationClaims.ImpersonatorEmail));
        Assert.Equal("Hilary",
            newPrincipal.FindFirstValue(ImpersonationClaims.ImpersonatedDisplayName));
    }

    [Fact]
    public void PreserveImpersonationClaims_WhenOldPrincipalHasNoImpersonationClaims_DoesNotModifyNewPrincipal()
    {
        var oldPrincipal = MakePrincipal();
        var newPrincipal = MakePrincipal();

        ImpersonationClaims.PreserveImpersonationClaims(oldPrincipal, newPrincipal);

        Assert.Null(newPrincipal.FindFirstValue(ImpersonationClaims.ImpersonatorEmail));
        Assert.Null(newPrincipal.FindFirstValue(ImpersonationClaims.ImpersonatedDisplayName));
    }

    [Fact]
    public void PreserveImpersonationClaims_WhenOldPrincipalIsNull_DoesNotThrow()
    {
        var newPrincipal = MakePrincipal();

        var ex = Record.Exception(
            () => ImpersonationClaims.PreserveImpersonationClaims(null, newPrincipal));

        Assert.Null(ex);
    }

    private static ClaimsPrincipal MakePrincipal(params (string Type, string Value)[] claims)
    {
        var claimList = claims.Select(c => new Claim(c.Type, c.Value));
        return new ClaimsPrincipal(new ClaimsIdentity(claimList, "TestAuth"));
    }
}

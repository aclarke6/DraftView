using DraftView.Web.Infrastructure;
using Microsoft.AspNetCore.Http;
using Moq;
using System.Security.Claims;
using Xunit;

namespace DraftView.Web.Tests;

public class HttpContextAuthorizationFacadeTests
{
    private static HttpContextAuthorizationFacade CreateFacade(ClaimsPrincipal user)
    {
        var httpContext = new DefaultHttpContext { User = user };
        var accessor = new Mock<IHttpContextAccessor>();
        accessor.Setup(a => a.HttpContext).Returns(httpContext);
        return new HttpContextAuthorizationFacade(accessor.Object);
    }

    private static ClaimsPrincipal UserWithRole(string role) =>
        new(new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, "test@example.com"),
             new Claim(ClaimTypes.Role, role)],
            "TestAuth"));

    [Fact]
    public void IsAuthor_WhenUserHasAuthorRole_ReturnsTrue()
    {
        var sut = CreateFacade(UserWithRole("Author"));
        Assert.True(sut.IsAuthor());
    }

    [Fact]
    public void IsAuthor_WhenUserHasOtherRole_ReturnsFalse()
    {
        var sut = CreateFacade(UserWithRole("BetaReader"));
        Assert.False(sut.IsAuthor());
    }

    [Fact]
    public void IsSystemSupport_WhenUserHasSystemSupportRole_ReturnsTrue()
    {
        var sut = CreateFacade(UserWithRole("SystemSupport"));
        Assert.True(sut.IsSystemSupport());
    }

    [Fact]
    public void IsBetaReader_WhenUserHasBetaReaderRole_ReturnsTrue()
    {
        var sut = CreateFacade(UserWithRole("BetaReader"));
        Assert.True(sut.IsBetaReader());
    }

    [Fact]
    public void GetCurrentUserEmail_ReturnsIdentityName()
    {
        var sut = CreateFacade(UserWithRole("Author"));
        Assert.Equal("test@example.com", sut.GetCurrentUserEmail());
    }

    [Fact]
    public void IsAuthor_WhenHttpContextIsNull_ReturnsFalse()
    {
        var accessor = new Mock<IHttpContextAccessor>();
        accessor.Setup(a => a.HttpContext).Returns((HttpContext?) null);
        var sut = new HttpContextAuthorizationFacade(accessor.Object);
        Assert.False(sut.IsAuthor());
    }
}
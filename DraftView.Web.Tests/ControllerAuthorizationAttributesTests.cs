using System.Linq;
using Xunit;

namespace DraftView.Web.Tests;

public class ControllerAuthorizationAttributesTests
{
    [Fact]
    public void AuthorController_HasRequireAuthorPolicyAttribute()
    {
        var type = typeof(DraftView.Web.Controllers.AuthorController);
        var attr = type.GetCustomAttributes(inherit: true)
            .OfType<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>()
            .FirstOrDefault();

        Assert.NotNull(attr);
        Assert.Equal("RequireAuthorPolicy", attr.Policy);
    }

    [Fact]
    public void DropboxController_HasRequireAuthorPolicyAttribute()
    {
        var type = typeof(DraftView.Web.Controllers.DropboxController);
        var attr = type.GetCustomAttributes(inherit: true)
            .OfType<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>()
            .FirstOrDefault();

        Assert.NotNull(attr);
        Assert.Equal("RequireAuthorPolicy", attr.Policy);
    }

    [Fact]
    public void BaseReaderController_HasBetaReaderAndAuthorRolesAttribute()
    {
        var type = typeof(DraftView.Web.Controllers.BaseReaderController);
        var attr = type.GetCustomAttributes(inherit: true)
            .OfType<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>()
            .FirstOrDefault();

        Assert.NotNull(attr);
        Assert.Equal("BetaReader,Author", attr.Roles);
    }

    [Fact]
    public void SupportController_HasSystemSupportRoleAttribute()
    {
        var type = typeof(DraftView.Web.Controllers.SupportController);
        var attr = type.GetCustomAttributes(inherit: true)
            .OfType<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>()
            .FirstOrDefault();

        Assert.NotNull(attr);
        Assert.Equal("SystemSupport", attr.Roles);
    }
}
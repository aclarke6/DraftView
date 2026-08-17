using DraftView.Web.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using System.Security.Claims;
using Xunit;

namespace DraftView.Web.Tests.Infrastructure;

/// <summary>
/// Tests for ReadOnlyImpersonationFilter.
/// Covers: pass-through for GET, block for POST when impersonating,
///         exemption via AllowWhenImpersonatingAttribute, pass-through without impersonation.
/// Excludes: sign-in swap logic (AuthorController), UI rendering (layout tests).
/// </summary>
public class ReadOnlyImpersonationFilterTests
{
    private static ActionExecutingContext MakeContext(
        string method,
        bool hasImpersonationClaim,
        bool hasExemptionAttribute = false)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = method;

        if (hasImpersonationClaim)
        {
            httpContext.User = new ClaimsPrincipal(
                new ClaimsIdentity(
                    [new Claim(ImpersonationClaims.ImpersonatorEmail, "author@example.test")],
                    "TestAuth"));
        }

        var actionDescriptor = new ControllerActionDescriptor();
        if (hasExemptionAttribute)
        {
            actionDescriptor.FilterDescriptors = new List<FilterDescriptor>
            {
                new FilterDescriptor(new AllowWhenImpersonatingAttribute(), FilterScope.Action)
            };
        }

        var actionContext = new ActionContext(httpContext, new RouteData(), actionDescriptor);
        return new ActionExecutingContext(
            actionContext,
            [],
            new Dictionary<string, object?>(),
            new object());
    }

    [Fact]
    public void OnActionExecuting_GetRequest_WhenImpersonating_PassesThrough()
    {
        var filter = new ReadOnlyImpersonationFilter();
        var context = MakeContext("GET", hasImpersonationClaim: true);

        filter.OnActionExecuting(context);

        Assert.Null(context.Result);
    }

    [Fact]
    public void OnActionExecuting_PostRequest_WhenNotImpersonating_PassesThrough()
    {
        var filter = new ReadOnlyImpersonationFilter();
        var context = MakeContext("POST", hasImpersonationClaim: false);

        filter.OnActionExecuting(context);

        Assert.Null(context.Result);
    }

    [Fact]
    public void OnActionExecuting_PostRequest_WhenImpersonating_SetsRedirectResult()
    {
        var filter = new ReadOnlyImpersonationFilter();
        var context = MakeContext("POST", hasImpersonationClaim: true);

        filter.OnActionExecuting(context);

        Assert.IsType<RedirectResult>(context.Result);
    }

    [Fact]
    public void OnActionExecuting_PostRequest_WhenImpersonatingWithExemption_PassesThrough()
    {
        var filter = new ReadOnlyImpersonationFilter();
        var context = MakeContext("POST", hasImpersonationClaim: true, hasExemptionAttribute: true);

        filter.OnActionExecuting(context);

        Assert.Null(context.Result);
    }
}

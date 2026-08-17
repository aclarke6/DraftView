using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace DraftView.Web.Infrastructure;

/// <summary>
/// Global action filter that blocks all non-GET requests while reader impersonation is active.
/// Actions decorated with AllowWhenImpersonatingAttribute are exempt (e.g. ExitImpersonation).
/// </summary>
public class ReadOnlyImpersonationFilter : IActionFilter
{
    /// <summary>
    /// Intercepts incoming requests and short-circuits non-GET calls during impersonation,
    /// unless the action carries AllowWhenImpersonatingAttribute.
    /// </summary>
    public void OnActionExecuting(ActionExecutingContext context)
    {
        if (!IsImpersonating(context))
            return;

        var method = context.HttpContext.Request.Method;
        if (HttpMethods.IsGet(method) || HttpMethods.IsHead(method))
            return;

        if (IsExempt(context))
            return;

        var referer = context.HttpContext.Request.Headers.Referer.ToString();
        context.Result = new RedirectResult(referer.Length > 0 ? referer : "/");
    }

    public void OnActionExecuted(ActionExecutedContext context) { }

    private static bool IsImpersonating(ActionExecutingContext context) =>
        context.HttpContext.User.HasClaim(c => c.Type == ImpersonationClaims.ImpersonatorEmail);

    private static bool IsExempt(ActionExecutingContext context) =>
        context.ActionDescriptor.FilterDescriptors
            .Any(fd => fd.Filter is AllowWhenImpersonatingAttribute);
}

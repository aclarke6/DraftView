using Microsoft.AspNetCore.Mvc.Filters;

namespace DraftView.Web.Infrastructure;

/// <summary>
/// Marks an action as exempt from ReadOnlyImpersonationFilter.
/// Apply only to actions that must work during reader impersonation (e.g. ExitImpersonation).
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class AllowWhenImpersonatingAttribute : Attribute, IFilterMetadata { }

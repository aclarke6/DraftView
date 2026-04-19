---
mode: agent
description: BUG-004 — ForgotPassword returns HTTP 405 and no custom error page
---

# BUG-004 — ForgotPassword returns HTTP 405 with no custom error page

## Branching
Branch from `BugFix-Mac` (or `BugFix-PC` if working on Windows):
`bugfix/BUG-004-forgotpassword-405`

## Problem
Navigating to `/Account/ForgotPassword` returns HTTP 405 Method Not Allowed.
The raw browser 405 page is shown — there is no custom 405 error page.

## Observed Behaviour
- User navigates to `/Account/ForgotPassword`
- Browser shows raw HTTP ERROR 405 page
- No custom DraftView error page shown

## Expected Behaviour
- `/Account/ForgotPassword` GET should show the forgot password form
- `/Account/ForgotPassword` POST should process the submission
- Any 405 error should be handled by the custom error middleware and show a
  friendly error page, not the raw browser error

## Root Cause (confirmed)
The controller code is correct — `[HttpGet]` and `[HttpPost]` are both present on `ForgotPassword`.
Nginx config is a clean proxy pass with no method restrictions.

The 405 is coming from ASP.NET Core routing. `Program.cs` has:

    app.UseStatusCodePagesWithReExecute("/Home/NotFoundPage");

This routes all non-success status codes to `/Home/NotFoundPage` — but that action
only handles 404 gracefully. A 405 from the routing layer hits `NotFoundPage` which
shows the wrong page. More critically, the raw browser 405 page is shown because
the status code middleware may not intercept routing-level 405s.

## Fix Required

### Fix 1 — Generalise status code handling in `Program.cs`
Change:

    app.UseStatusCodePagesWithReExecute("/Home/NotFoundPage");

To:

    app.UseStatusCodePagesWithReExecute("/Home/Error", "?statusCode={0}");

### Fix 2 — Update `HomeController.Error` to accept a status code
Read `DraftView.Web/Controllers/HomeController.cs` and `Views/Shared/Error.cshtml`.
Add an optional `statusCode` parameter and show an appropriate user-friendly message:
- 404 → "Page not found"
- 405 → "That action is not allowed"
- 500 → "Something went wrong"
- default → "An unexpected error occurred"

### Fix 3 — Remove or repurpose `NotFoundPage`
If `NotFoundPage` is only used for 404s, it can remain as a dedicated 404 view
called from the Error action. Or it can be removed if Error handles all cases.

## Rules
- Deploy latest build and retest before fixing — may already be resolved
- Reproduce and confirm root cause before changing any production code
- TDD where applicable
- Fix issue 1 and issue 2 as separate commits

## Commits
- `bugfix: BUG-004a — fix ForgotPassword route/method mismatch`
- `bugfix: BUG-004b — add 405 handling to error middleware`
- Merge back to `BugFix-Mac` or `BugFix-PC` when complete
